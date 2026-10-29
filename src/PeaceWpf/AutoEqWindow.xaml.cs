using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PeaceWpf.Core.AutoEq;
using Wpf.Ui.Controls;

namespace PeaceWpf;

/// <summary>
/// AutoEQ 选择窗口：挑型号 → 看 EqualizerAPO 命令 → 一键套到「左右」层。
///
/// 数据是 Peace 自带的那四份结果库，放在程序目录的 AutoEqData 下，打开窗口才读，
/// 读完缓存在 <see cref="AutoEqCatalog"/> 里，切库不再重新解析。
/// </summary>
public partial class AutoEqWindow : FluentWindow
{
    private readonly Func<AutoEqProfile, Task<string>> _apply;
    private readonly Func<AutoEqProfile, Task<string>> _create;
    private readonly DispatcherTimer _searchTimer;

    private AutoEqSource? _source;
    private AutoEqDatabase? _database;
    private AutoEqProfile? _profile;

    public AutoEqWindow(Func<AutoEqProfile, Task<string>> apply, Func<AutoEqProfile, Task<string>> create)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _create = create ?? throw new ArgumentNullException(nameof(create));

        InitializeComponent();

        // 尺寸钉死之后没有可拉的边框，顶部留一条能按住拖窗口
        WindowDragHelper.Enable(this);

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            FillList();
        };

        DatabaseBox.ItemsSource = AutoEqCatalog.Sources;
        DatabaseBox.DisplayMemberPath = nameof(AutoEqSource.DisplayName);
        TypeBox.ItemsSource = AutoEqTypes.DisplayNames;
        TypeBox.SelectedIndex = 0;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (!AutoEqCatalog.IsAvailable())
        {
            StatusText.Text = $"没找到 AutoEQ 数据：把 Peace 的 AutoEQCompressed5.7z 解到程序目录的 "
                + $"{AutoEqCatalog.DataFolderName} 里（四个 .txt 都要）。";
            DatabaseBox.IsEnabled = false;
            SearchBox.IsEnabled = false;
            TypeBox.IsEnabled = false;
            PreviewBox.Text = "没有数据就画不出曲线；不过你可以直接把 EqualizerAPO 的命令粘进 peace.txt，本程序会照样读回来。";
            return;
        }

        DatabaseBox.SelectedIndex = 0;
        await LoadDatabaseAsync();
    }

    private async void OnDatabaseChanged(object sender, SelectionChangedEventArgs e)
    {
        _source = DatabaseBox.SelectedItem as AutoEqSource;
        await LoadDatabaseAsync();
    }

    private async Task LoadDatabaseAsync()
    {
        if (_source is null)
        {
            return;
        }

        var source = _source;
        StatusText.Text = $"正在读 {source.DisplayName}…";
        DeviceList.ItemsSource = null;
        SummaryText.Text = string.Empty;
        PreviewBox.Text = string.Empty;
        ApplyButton.IsEnabled = false;
        _database = null;
        _profile = null;

        var (database, error) = await Task.Run(() =>
        {
            var loaded = AutoEqCatalog.TryLoad(source, out var message);
            return (Database: loaded, Error: message);
        });

        if (!ReferenceEquals(_source, source))
        {
            // 读的时候用户又换了库，这次结果作废。
            return;
        }

        if (database is null)
        {
            StatusText.Text = error;
            return;
        }

        _database = database;
        FillList();
    }

    private void FillList()
    {
        if (_database is null)
        {
            return;
        }

        var matches = _database.Search(SearchBox.Text, 800);
        DeviceList.ItemsSource = matches;

        if (matches.Count > 0)
        {
            DeviceList.SelectedIndex = 0;
        }
        else
        {
            DeviceList.SelectedIndex = -1;
            UpdatePreview();
        }

        var capped = matches.Count >= 800 && !string.IsNullOrWhiteSpace(SearchBox.Text)
            ? "（只显示前 800 台，缩一下搜索词）"
            : string.Empty;
        StatusText.Text = $"{_source?.DisplayName} · 共 {_database.Devices.Count} 台 · 命中 {matches.Count} 台{capped}";
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnDeviceChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void OnTypeChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (_database is null)
        {
            return;
        }

        var device = DeviceList.SelectedItem as AutoEqDevice;
        var type = TypeBox.SelectedIndex >= 0 ? (AutoEqType)TypeBox.SelectedIndex : AutoEqType.Parametric;
        _profile = _database.GetProfile(device, type);

        if (_profile is null)
        {
            PreviewBox.Text = string.Empty;
            PreviewGraph.EmptyText = device is null
                ? "选一台设备看曲线"
                : "这台设备没有这种形态的等化";
            PreviewGraph.SetCurve(null);
            SummaryText.Text = device is null
                ? string.Empty
                : "这台设备在这个库里没有这种形态的等化，换一种形态试试。";
            ApplyButton.IsEnabled = false;
            CreateButton.IsEnabled = false;
            return;
        }

        PreviewBox.Text = _profile.ToEqualizerApoText();
        PreviewGraph.EmptyText = "这条等化没有可画的曲线";
        PreviewGraph.SetCurve(
            _profile.SampleResponse(260),
            _profile.FilterFrequencies.Count > 0 ? _profile.FilterFrequencies : null);

        SummaryText.Text = $"{_profile.Device.Display}｜{_profile.Summary}";
        ApplyButton.IsEnabled = _profile.CanApplyToPoints;
        CreateButton.IsEnabled = _profile.CanApplyToPoints;
        ApplyButton.ToolTip = _profile.CanApplyToPoints
            ? "替换「左右」层曲线并设好预增益；在主窗口可以撤回"
            : "图形 EQ 是卷积曲线，点模型写不进去；换「参数 EQ」或「固定频带 EQ」再应用";
        CreateButton.ToolTip = _profile.CanApplyToPoints
            ? "把这份等化另存成一条新曲线，名字默认用当前预设名；不动当前层"
            : "图形 EQ 是卷积曲线，点模型写不进去；换「参数 EQ」或「固定频带 EQ」再创建";
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        ApplyButton.IsEnabled = false;
        try
        {
            StatusText.Text = await _apply(_profile);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText.Text = "套用失败：" + ex.Message;
        }
        finally
        {
            ApplyButton.IsEnabled = _profile.CanApplyToPoints;
        }
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        if (_profile is null)
        {
            return;
        }

        CreateButton.IsEnabled = false;
        try
        {
            StatusText.Text = await _create(_profile);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            StatusText.Text = "创建新曲线失败：" + ex.Message;
        }
        finally
        {
            CreateButton.IsEnabled = _profile.CanApplyToPoints;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
