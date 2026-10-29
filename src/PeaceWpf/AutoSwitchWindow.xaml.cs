using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PeaceWpf.Core.Model;
using PeaceWpf.Services;
using PeaceWpf.ViewModels;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// 「自动切换」：组合列表，一组一行。
///
/// 这个页面上没有「保存」和「取消」：改一下、加一行、删一行、录个热键，全都立刻落盘，
/// 也不做撤销。热键和进程监听由 <see cref="AutoSwitchService"/> 在后台盯着。
/// </summary>
public partial class AutoSwitchWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly AutoSwitchService? _service;
    private AutoSwitchRow? _captureRow;
    private AutoSwitchRow? _dragRow;
    private Point _dragStart;
    private bool _dragMoved;
    private string? _inputNote;

    public AutoSwitchWindow(MainViewModel viewModel, AutoSwitchService? service = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _service = service;

        InitializeComponent();
        DataContext = this;
        WindowDragHelper.Enable(this);

        BuildPresetChoices();
        RowsList.ItemsSource = Rows;

        foreach (var profile in _viewModel.AutoSwitchProfiles.Where(item => item is not null).ToList())
        {
            var row = new AutoSwitchRow(profile);
            Subscribe(row);
            Rows.Add(row);

            if (IsReady(row))
            {
                _readyRows.Add(row);
            }
        }

        PreviewKeyDown += OnPreviewKeyDown;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _presetSnapshot = _viewModel.AvailablePresets.ToList();
        DropMissingPresets();
        SyncVoiceWakeRows();
        Activated += OnWindowActivated;

        if (_service is not null)
        {
            _service.HotkeysChanged += OnHotkeysChanged;

            // 设置页开着的时候把全局热键全摘掉，免得录键 / 改组合时按到别的热键就跳曲线。
            _service.SuspendHotkeys();
        }

        Closed += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (_service is not null)
            {
                _service.HotkeysChanged -= OnHotkeysChanged;
                _service.ResumeHotkeys();
            }
        };

        UpdateEmptyHint();
        RefreshRowOrder();
        RefreshActive();
        RefreshConflicts();
    }

    /// <summary>列表里的行，顺序就是存盘顺序。</summary>
    public ObservableCollection<AutoSwitchRow> Rows { get; } = new();

    /// <summary>两个下拉共用的预设名；组合里引用的名字即使文件没了也留着，免得一打开就变成空。</summary>
    public ObservableCollection<string> PresetNames { get; } = new();

    private void BuildPresetChoices()
    {
        PresetNames.Clear();

        foreach (var name in _viewModel.AvailablePresets)
        {
            AddPresetName(name);
        }

        AddPresetName(_viewModel.MainPresetName);
        AddPresetName(_viewModel.BassPresetName);
    }

    /// <summary>
    /// 组合里引用的预设如果已经不在了（被删或改名），把那一格清成空，
    /// 下拉框就回到「未选择」，而不是留着一个点不开的名字。
    /// </summary>
    private void DropMissingPresets()
    {
        foreach (var row in Rows)
        {
            if (!string.IsNullOrWhiteSpace(row.MainPreset) && !HasPreset(row.MainPreset))
            {
                row.MainPreset = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(row.BassPreset) && !HasPreset(row.BassPreset))
            {
                row.BassPreset = string.Empty;
            }
        }
    }

    private bool HasPreset(string name) =>
        PresetNames.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 下拉框上右键：直接把这一格清成「未选择」，不弹菜单。
    /// 两条都清空时这一行不再指向任何东西，激活状态一并取消。
    /// </summary>
    private void OnPresetRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBox { DataContext: AutoSwitchRow row } combo)
        {
            return;
        }

        e.Handled = true;

        if (combo.Tag as string == "bass")
        {
            row.BassPreset = string.Empty;
        }
        else
        {
            row.MainPreset = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(row.MainPreset) && string.IsNullOrWhiteSpace(row.BassPreset))
        {
            // 什么都没指定了：以后重新选齐时还要能再套用一次。
            _readyRows.Remove(row);
        }

        RefreshActive();
    }

    /// <summary>上次看到的预设清单，用来判断窗口重新激活时要不要重建下拉项。</summary>
    private List<string> _presetSnapshot = new();

    /// <summary>
    /// 窗口重新拿到焦点时对一下预设清单：期间删了或改了名，就重建下拉项，
    /// 并把指向已消失预设的格子清空。内容没变就什么都不做，免得下拉框闪。
    /// </summary>
    private void OnWindowActivated(object? sender, EventArgs e)
    {
        var current = _viewModel.AvailablePresets.ToList();
        if (current.Count == _presetSnapshot.Count
            && current.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(_presetSnapshot.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        _presetSnapshot = current;
        BuildPresetChoices();
        DropMissingPresets();
        RefreshActive();
    }

    private void AddPresetName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!PresetNames.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase)))
        {
            PresetNames.Add(name);
        }
    }

    private readonly HashSet<AutoSwitchRow> _readyRows = new();

    private void Subscribe(AutoSwitchRow row) => row.Changed += (_, _) => OnRowChanged(row);

    /// <summary>
    /// 改了一行的设置：先存盘，再按「这一行」重算激活。
    ///
    /// 改的是激活行时，只认它现在这份设置：
    ///   · 和它一样的行 → 一起激活（改回一样时，原本一样的也会亮）；
    ///   · 和它不一样的行 → 全部取消（改得不一样了，就只亮这一个不一样的）。
    /// 改的是没激活的行时：
    ///   · 如果改完和某个激活行一样了 → 这一组（包括它）一起激活；
    ///   · 否则激活状态一点不动。
    /// </summary>
    private void OnRowChanged(AutoSwitchRow row)
    {
        var wasActive = row.IsActive;
        Save();

        // 刚把左右和低音两条曲线都选齐：这一组立刻套用并激活。
        // 第一次选的时候另一条还是空的，不算选齐。
        if (IsReady(row) && _readyRows.Add(row))
        {
            var ok = _viewModel.ApplyAutoSwitch(row.Data, out var message);
            RefreshConflicts();

            if (ok)
            {
                RefreshActive();
            }
            else
            {
                ShowError(message);
            }

            return;
        }

        if (wasActive)
        {
            SetActiveRows(Rows.Where(item => SameCurves(item, row)));
            return;
        }

        // 没激活的行改成了某个激活行的设置：和它一样的那些也要亮起来。
        var activePeer = Rows.FirstOrDefault(item => item.IsActive && SameCurves(item, row));
        if (activePeer is not null)
        {
            SetActiveRows(Rows.Where(item => SameCurves(item, activePeer)));
        }
    }

    /// <summary>至少选中了一层就能激活：空的那层表示「不改变对应配置」。</summary>
    private static bool IsReady(AutoSwitchRow row) =>
        !string.IsNullOrWhiteSpace(row.MainPreset) || !string.IsNullOrWhiteSpace(row.BassPreset);

    /// <summary>
    /// 这个页面的「立即保存」：写盘 + 重挂热键，然后刷新冲突提示。
    /// 注意不重算「激活」——改一行的设置不该把它自己取消激活。
    /// </summary>
    private void Save()
    {
        _viewModel.SaveAutoSwitchProfiles();
        RefreshConflicts();
        SyncVoiceWakeRows();
    }

    /// <summary>
    /// 按存盘的三元组刷新「语音唤醒」那列。
    /// 同三元组的多行会一起亮：它们本来就是同一个组合（复制出来的、只差叠加开关的）。
    /// 绑定的组合被删光了就顺手清掉，免得留下一个看不见的绑定。
    /// </summary>
    private void SyncVoiceWakeRows()
    {
        var key = _viewModel.VoiceWakeKey;
        var matched = false;
        foreach (var row in Rows)
        {
            var on = key is not null && string.Equals(row.Data.ComboKey, key, StringComparison.Ordinal);
            row.IsVoiceWake = on;
            matched |= on;
        }

        if (key is not null && !matched)
        {
            _viewModel.VoiceWakeKey = null;
        }
    }

    /// <summary>点「语音唤醒」：全局只能开一个，再点一次就是关掉。</summary>
    private void OnVoiceWakeClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        var turnOn = !row.IsVoiceWake;
        _viewModel.VoiceWakeKey = turnOn ? row.Data.ComboKey : null;
        SyncVoiceWakeRows();
    }

    private void OnAddRowClicked(object sender, RoutedEventArgs e)
    {
        // 新组合先留空：左右和低音都显示「未选择」，等用户自己挑。
        var data = new UiAutoSwitch
        {
            MainPreset = string.Empty,
            BassPreset = string.Empty,
            BassEnabled = true,
        };

        _viewModel.AutoSwitchProfiles.Add(data);
        var row = new AutoSwitchRow(data);
        Subscribe(row);
        Rows.Add(row);

        Save();
        UpdateEmptyHint();
        RefreshRowOrder();
        RowsList.UpdateLayout();
    }

    private void OnDeleteRowClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        if (_captureRow == row)
        {
            row.CancelCapture();
            _captureRow = null;
        }

        _viewModel.AutoSwitchProfiles.Remove(row.Data);
        Rows.Remove(row);

        Save();
        UpdateEmptyHint();
        RefreshRowOrder();
    }

    /// <summary>
    /// 教程用：把一组演示组合挂到列表里，不写盘。
    /// 演示行由主窗口在教程最后一步创建，结束时再调 <see cref="RemoveTutorialRow"/> 摘掉。
    /// </summary>
    internal void AddTutorialRow(UiAutoSwitch data)
    {
        if (Rows.Any(row => ReferenceEquals(row.Data, data)))
        {
            return;
        }

        var row = new AutoSwitchRow(data);
        Subscribe(row);
        Rows.Add(row);
        UpdateEmptyHint();
        RefreshRowOrder();
        RefreshActive();
        RowsList.UpdateLayout();
    }

    /// <summary>教程用：把演示行从列表里摘掉，不写盘。</summary>
    internal void RemoveTutorialRow(UiAutoSwitch data)
    {
        var row = Rows.FirstOrDefault(item => ReferenceEquals(item.Data, data));
        if (row is null)
        {
            return;
        }

        Rows.Remove(row);
        UpdateEmptyHint();
        RefreshRowOrder();
        RefreshActive();
    }

    // ---- 热键：单击进入录键，框里出现勾和叉 ----

    private void OnHotkeyCellMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (RowOf(sender) is not { } row || row.IsCapturing)
        {
            return;
        }

        // 勾和叉是框里的按钮，点它们不算「重新开始录」。
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        _captureRow?.CancelCapture();
        _captureRow = row;
        ClearInputNote();
        row.BeginCapture();
        Focus();
        e.Handled = true;
    }

    private void OnHotkeyCommitClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        _captureRow = row;
        EndCapture(cancel: false);
        e.Handled = true;
    }

    private void OnHotkeyCancelClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        if (_captureRow == row)
        {
            _captureRow = null;
        }

        row.CancelCapture();
        ClearInputNote();
        e.Handled = true;
    }

    private void OnHotkeyClearClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        row.ClearHotkey();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_captureRow is null)
        {
            // 不录键时 Esc 就关窗，和右上角那个叉一个意思。
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }

            return;
        }

        // Alt 组合在 WPF 里走 SystemKey，真正的键在那里。
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 输入法吃掉的那几个不当作热键。
        if (key is Key.ImeProcessed or Key.DeadCharProcessed)
        {
            return;
        }

        e.Handled = true;

        if (key == Key.Escape)
        {
            _captureRow.CancelCapture();
            _captureRow = null;
            return;
        }

        if (key == Key.Enter)
        {
            EndCapture(cancel: false);
            return;
        }

        if (HotkeyFormat.IsModifierKey(key))
        {
            _captureRow.NoteModifiers(Keyboard.Modifiers);
            return;
        }

        // 按下就当查一次重：曲线相同的两行不许共用一把热键。
        // 撞了就把这次输入作废 —— 框里清空，人接着按下一个键就行。
        var modifiers = HotkeyFormat.ToHotkeyModifiers(Keyboard.Modifiers);
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (FindHotkeyOwner(_captureRow, modifiers, virtualKey) is { } owner)
        {
            _captureRow.ClearCaptureDraft();
            _inputNote = $"「{HotkeyFormat.Format(modifiers, virtualKey)}」和第 {RowNumber(owner)} 组撞了：两行曲线一样，热键不能一样。这次没记下来，换一个组合键。";
            UpdateErrors();
            return;
        }

        ClearInputNote();
        _captureRow.Capture(key, Keyboard.Modifiers);
    }

    private void EndCapture(bool cancel)
    {
        if (_captureRow is not { } row)
        {
            return;
        }

        if (cancel)
        {
            _captureRow = null;
            row.CancelCapture();
            ClearInputNote();
            return;
        }

        // 勾之前再核一次：录的时候没撞，不代表这会儿还没撞（别的行可能刚改过曲线）。
        if (row.TryGetCaptureKey(out var modifiers, out var virtualKey)
            && FindHotkeyOwner(row, modifiers, virtualKey) is { } owner)
        {
            row.ClearCaptureDraft();
            _inputNote = $"「{HotkeyFormat.Format(modifiers, virtualKey)}」和第 {RowNumber(owner)} 组撞了：两行曲线一样，热键不能一样。这次没记下来，换一个组合键。";
            UpdateErrors();
            return;
        }

        _captureRow = null;
        row.CommitCapture();
        ClearInputNote();
    }

    // ---- 进程绑定：黑罩 + 蓝框 ----

    private void OnProcessCellMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        // 那个小叉是「解除绑定」，不能同时把人带进选择模式。
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        e.Handled = true;
        PickProcess(row);
    }

    private void OnProcessClearClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        row.ClearProcess();
        e.Handled = true;
    }

    /// <summary>
    /// 绑进程时把弹窗和主界面都收起来，不然它们会盖住要选的那个窗口。
    /// 选完再原样放回来。
    /// </summary>
    private void PickProcess(AutoSwitchRow row)
    {
        _captureRow?.CancelCapture();
        _captureRow = null;
        ClearInputNote();

        var owner = Owner;
        Hide();
        owner?.Hide();

        try
        {
            var picker = new ProcessPickWindow();
            if (picker.ShowDialog() != true || string.IsNullOrWhiteSpace(picker.PickedProcess))
            {
                return;
            }

            // 选完当场查重：曲线相同的两行不许绑同一个进程。
            var picked = picker.PickedProcess;
            if (FindProcessOwner(row, picked) is { } peer)
            {
                _inputNote = $"{picked} 和第 {RowNumber(peer)} 组撞了：两行曲线一样，进程不能绑同一个。这次没绑上，换一个进程。";
                UpdateErrors();
                return;
            }

            row.SetProcess(picked);
            ClearInputNote();
        }
        finally
        {
            owner?.Show();
            Show();
            Activate();
        }
    }

    // ---- 收尾 ----

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnHotkeysChanged(object? sender, EventArgs e) => UpdateErrors();

    private void UpdateErrors()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_inputNote))
        {
            parts.Add(_inputNote);
        }

        if (!string.IsNullOrWhiteSpace(_conflictNote))
        {
            parts.Add(_conflictNote);
        }

        parts.AddRange(_service?.HotkeyErrors ?? Array.Empty<string>());
        ShowError(parts.Count == 0 ? null : string.Join(" ", parts));
    }

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? string.Empty;
        ErrorText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- 当前组合：勾选框 + 蓝色高亮 ----

    /// <summary>声音现在正好是哪一组，就给哪些行打勾、描蓝框；相同的组合会一起亮。</summary>
    private void RefreshActive() => SetActiveRows(Rows.Where(MatchesCurrent));

    /// <summary>
    /// 声音现在正好是哪一组，就给哪些行打勾、描蓝框；相同的组合会一起亮。
    /// 空着的格子是通配：那一层不归这一行管，所以不参与比对。
    /// </summary>
    private bool MatchesCurrent(AutoSwitchRow row) =>
        // 两条都空的行没指定任何东西，不算「正在用这一组」。
        (!string.IsNullOrWhiteSpace(row.MainPreset) || !string.IsNullOrWhiteSpace(row.BassPreset))
        && (string.IsNullOrWhiteSpace(row.MainPreset)
            || string.Equals(row.MainPreset, _viewModel.MainPresetName, StringComparison.OrdinalIgnoreCase))
        && (string.IsNullOrWhiteSpace(row.BassPreset)
            || (string.Equals(row.BassPreset, _viewModel.BassPresetName, StringComparison.OrdinalIgnoreCase)
                && row.BassEnabled == _viewModel.IsBassLayerEnabled));

    private void SetActiveRows(IEnumerable<AutoSwitchRow> active)
    {
        var set = new HashSet<AutoSwitchRow>(active);
        foreach (var row in Rows)
        {
            row.IsActive = set.Contains(row);
        }
    }

    private void OnActivateRowClicked(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        var ok = _viewModel.ApplyAutoSwitch(row.Data, out var message);
        RefreshConflicts();

        if (!ok)
        {
            // 具体的失败原因比「有重复」更要紧，先让人看到它。
            ShowError(message);
        }
        else
        {
            // 切换成功后按新的声音状态重算：相同曲线的行会一起激活。
            RefreshActive();
        }

        // 勾选框是单向往数据走：点下去的那一下本地会先变。
        // 这里必须用 SetCurrentValue —— 直接赋值会把绑定顶掉，之后高亮没了也推不回勾。
        if (sender is CheckBox box)
        {
            box.SetCurrentValue(CheckBox.IsCheckedProperty, row.IsActive);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.SelectedMainPreset)
            or nameof(MainViewModel.SelectedBassPreset)
            or nameof(MainViewModel.IsBassLayerEnabled))
        {
            RefreshActive();
        }
    }

    // ---- 重复组合：两条曲线的叠加相同、热键或进程又绑成同一个 ----

    private string? _conflictNote;

    /// <summary>
    /// 曲线（左右 + 低音 + 叠不叠加）完全相同的两行，如果热键或进程也撞了，
    /// 就把撞到的那一格描红 —— 不然按热键到底切哪一组、切到进程时听谁的都说不清。
    /// </summary>
    private void RefreshConflicts()
    {
        foreach (var row in Rows)
        {
            row.HasHotkeyConflict = false;
            row.HasProcessConflict = false;
        }

        var hotkeyConflict = false;
        var processConflict = false;

        for (var i = 0; i < Rows.Count; i++)
        {
            for (var j = i + 1; j < Rows.Count; j++)
            {
                var a = Rows[i];
                var b = Rows[j];
                if (!SameCurves(a, b))
                {
                    continue;
                }

                if (a.HasHotkey && b.HasHotkey
                    && a.Data.HotkeyModifiers == b.Data.HotkeyModifiers
                    && a.Data.HotkeyVirtualKey == b.Data.HotkeyVirtualKey)
                {
                    a.HasHotkeyConflict = true;
                    b.HasHotkeyConflict = true;
                    hotkeyConflict = true;
                }

                if (a.HasProcess && b.HasProcess
                    && string.Equals(a.Data.ProcessName, b.Data.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    a.HasProcessConflict = true;
                    b.HasProcessConflict = true;
                    processConflict = true;
                }
            }
        }

        _conflictNote = (hotkeyConflict, processConflict) switch
        {
            (true, true) => "红框的两处重复了：曲线相同，热键和进程都绑成了同一个。",
            (true, false) => "红框的热键重复了：两行曲线相同，热键也一样。",
            (false, true) => "红框的进程重复了：两行曲线相同，进程也一样。",
            _ => null,
        };

        UpdateErrors();
    }

    private static bool SameCurves(AutoSwitchRow a, AutoSwitchRow b) =>
        string.Equals(a.MainPreset, b.MainPreset, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.BassPreset, b.BassPreset, StringComparison.OrdinalIgnoreCase)
        && a.BassEnabled == b.BassEnabled;

    // ---- 输入即查重：曲线相同的两行不许共用一把热键 / 一个进程 ----

    /// <summary>清掉上一次「输入被拒」的提示；换成别的动作时顺手抹掉。</summary>
    private void ClearInputNote()
    {
        if (_inputNote is null)
        {
            return;
        }

        _inputNote = null;
        UpdateErrors();
    }

    /// <summary>曲线相同、热键也一样的另一行；没有就返回 null。</summary>
    private AutoSwitchRow? FindHotkeyOwner(AutoSwitchRow row, HotkeyModifiers modifiers, int virtualKey) =>
        virtualKey <= 0
            ? null
            : Rows.FirstOrDefault(other => !ReferenceEquals(other, row)
                && SameCurves(other, row)
                && other.HasHotkey
                && other.Data.HotkeyModifiers == modifiers
                && other.Data.HotkeyVirtualKey == virtualKey);

    /// <summary>曲线相同、进程也绑成同一个的另一行；没有就返回 null。</summary>
    private AutoSwitchRow? FindProcessOwner(AutoSwitchRow row, string? processName) =>
        string.IsNullOrWhiteSpace(processName)
            ? null
            : Rows.FirstOrDefault(other => !ReferenceEquals(other, row)
                && SameCurves(other, row)
                && other.HasProcess
                && string.Equals(TrimExe(other.Data.ProcessName!), TrimExe(processName), StringComparison.OrdinalIgnoreCase));

    private static string TrimExe(string name)
    {
        var trimmed = name.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }

    /// <summary>给提示用的「第几组」，从 1 开始。</summary>
    private int RowNumber(AutoSwitchRow row) => Rows.IndexOf(row) + 1;

    // ---- 顺序：拖动手柄 + 序号。热键 / 进程重复时，排在前面的那一组生效 ----

    /// <summary>序号从 1 重排一遍。列表顺序就是优先级，也是存盘顺序。</summary>
    private void RefreshRowOrder()
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            Rows[i].Order = i + 1;
        }
    }

    private void OnDragHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (RowOf(sender) is not { } row)
        {
            return;
        }

        _dragRow = row;
        _dragStart = e.GetPosition(RowsList);
        (sender as UIElement)?.CaptureMouse();
        e.Handled = true;
    }

    private void OnDragHandleMouseUp(object sender, MouseButtonEventArgs e)
    {
        (sender as UIElement)?.ReleaseMouseCapture();
        _dragRow = null;
    }

    private void OnDragHandleMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragRow is not { } row)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            (sender as UIElement)?.ReleaseMouseCapture();
            _dragRow = null;
            return;
        }

        // 挪过几个像素才算拖：单击手柄不该把行带走。
        var position = e.GetPosition(RowsList);
        if (Math.Abs(position.Y - _dragStart.Y) < 6)
        {
            return;
        }

        _dragRow = null;
        (sender as UIElement)?.ReleaseMouseCapture();

        _dragMoved = false;
        row.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop(RowsList, new DataObject(typeof(AutoSwitchRow), row), DragDropEffects.Move);
        }
        finally
        {
            row.IsDragging = false;

            // 拖动过程中只挪界面；松手这里才落盘、才按新顺序重挂热键。
            if (_dragMoved)
            {
                Save();
            }
        }
    }

    /// <summary>
    /// 拖动经过别的行：当场把这一行挪过去，松手之前就能看到它会落在哪。
    /// 这一步不写盘 —— 每一步都存盘会把热键反复摘挂一遍，只在松手时存一次。
    /// </summary>
    private void OnRowsDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(AutoSwitchRow)) is not AutoSwitchRow dragged)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;

        if (TryGetDropSlot(e.GetPosition(RowsList), dragged) is { } slot)
        {
            var from = Rows.IndexOf(dragged);
            if (from >= 0)
            {
                // slot 是「摘出去之前」的下标，摘掉之后要前移一格。
                var to = slot > from ? slot - 1 : slot;
                if (to != from)
                {
                    MoveRow(from, to);
                    _dragMoved = true;
                }
            }
        }

        e.Handled = true;
    }

    private void OnRowsDrop(object sender, DragEventArgs e)
    {
        // 位置在拖动过程中已经挪好了，这里只收尾，不再改顺序。
        if (e.Data.GetDataPresent(typeof(AutoSwitchRow)))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 鼠标指着的位置对应「插到第几格之前」；落在某行上半 → 插到它前面，下半 → 插到它后面。
    /// 落在被拖的那一行自己身上返回 null（不用动）。
    /// </summary>
    private int? TryGetDropSlot(Point position, AutoSwitchRow dragged)
    {
        var index = 0;
        var slot = -1;
        var hitDragged = false;

        foreach (var item in Rows)
        {
            if (RowsList.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container)
            {
                index++;
                continue;
            }

            var bounds = container
                .TransformToAncestor(RowsList)
                .TransformBounds(new Rect(default, container.RenderSize));

            if (position.Y <= bounds.Top)
            {
                slot = index;
                break;
            }

            if (position.Y <= bounds.Bottom)
            {
                hitDragged = ReferenceEquals(item, dragged);
                slot = position.Y > bounds.Top + bounds.Height / 2 ? index + 1 : index;
                break;
            }

            index++;
        }

        if (slot < 0)
        {
            // 落在最后一行下面：挪到末尾。
            slot = Rows.Count;
        }

        return hitDragged ? null : slot;
    }

    /// <summary>
    /// 把一行挪到新位置：只动界面和内存，写盘与重挂热键留到松手时做一次。
    /// 数据那边找不到这一行就什么都不动，免得界面和存盘的数据对不上。
    /// </summary>
    private void MoveRow(int from, int to)
    {
        if (from < 0 || from >= Rows.Count || to < 0 || to >= Rows.Count || from == to)
        {
            return;
        }

        var row = Rows[from];
        var profiles = _viewModel.AutoSwitchProfiles;

        if (!profiles.Remove(row.Data))
        {
            return;
        }

        Rows.RemoveAt(from);
        Rows.Insert(to, row);
        profiles.Insert(Math.Min(to, profiles.Count), row.Data);

        RefreshRowOrder();
    }

    // ---- 右键：复制组合（只复制曲线和叠加开关） ----

    private void OnCopyRowClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem item)
        {
            return;
        }

        var row = item.DataContext as AutoSwitchRow;
        if (row is null && item.Parent is ContextMenu menu && menu.PlacementTarget is FrameworkElement target)
        {
            row = target.DataContext as AutoSwitchRow;
        }

        if (row is null)
        {
            return;
        }

        // 热键和进程绑定不复制：复制出来的那行要自己决定怎么触发。
        var data = new UiAutoSwitch
        {
            MainPreset = row.Data.MainPreset,
            BassPreset = row.Data.BassPreset,
            BassEnabled = row.Data.BassEnabled,
        };

        var index = Math.Max(0, Rows.IndexOf(row) + 1);
        _viewModel.AutoSwitchProfiles.Insert(Math.Min(index, _viewModel.AutoSwitchProfiles.Count), data);

        var copy = new AutoSwitchRow(data);
        Subscribe(copy);
        copy.IsActive = MatchesCurrent(copy);
        Rows.Insert(Math.Min(index, Rows.Count), copy);

        if (IsReady(copy))
        {
            _readyRows.Add(copy);
        }

        Save();
        UpdateEmptyHint();
        RefreshRowOrder();
    }

    private void UpdateEmptyHint()
    {
        var empty = Rows.Count == 0;
        EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        AddButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private static AutoSwitchRow? RowOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as AutoSwitchRow;

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match)
            {
                return match;
            }

            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }

        return null;
    }
}
