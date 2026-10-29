using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PeaceWpf.Services;

/// <summary>
/// 托盘图标：程序一启动就挂在托盘上，双击 / 左键 / 菜单都能把主界面叫回来。
///
/// 用 WinForms 的 NotifyIcon，是因为 WPF 自己没有托盘 API，而 NotifyIcon 就在
/// WindowsDesktop 运行时里，不用再引第三方包。菜单自己画成深色，跟主界面一套配色。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string IconResource = "Assets/EasyEQ.ico";

    private readonly Forms.NotifyIcon _icon;

    /// <summary>Icon(Stream) 要求流活到图标结束，不能读完就关。</summary>
    private readonly Stream? _iconStream;

    private bool _disposed;

    /// <summary>左键单击、或菜单里的「打开主界面」。</summary>
    public event EventHandler? OpenRequested;

    /// <summary>菜单里的「设置…」。</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>菜单里的「退出软件」。</summary>
    public event EventHandler? ExitRequested;

    public TrayIconService(string title)
    {
        _iconStream = LoadIconStream();

        _icon = new Forms.NotifyIcon
        {
            // 托盘提示最多 63 个字符，这里只有四个字，不会被截断。
            Text = title,
            Icon = LoadIcon(),
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };

        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    /// <summary>
    /// 读 exe 里的图标资源。资源本来就该在；万一读不到也不该让托盘把程序拖垮，
    /// 返回 null 之后会退到 exe 自带的图标。
    /// </summary>
    private static Stream? LoadIconStream()
    {
        try
        {
            return System.Windows.Application.GetResourceStream(
                new Uri(IconResource, UriKind.Relative))?.Stream;
        }
        catch (Exception ex) when (ex is IOException or UriFormatException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>第一次收进托盘时冒个气泡，告诉用户程序没退、去哪儿找。</summary>
    public void ShowHint(string title, string message)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.ShowBalloonTip(3000);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // 气泡只是提示，弹不出来不影响托盘图标本身。
        }
    }

    private Icon? LoadIcon()
    {
        if (_iconStream is not null)
        {
            try
            {
                return new Icon(_iconStream);
            }
            catch (ArgumentException)
            {
                // 资源不是合法 .ico 就退到 exe 自带的图标。
            }
        }

        try
        {
            return string.IsNullOrEmpty(Environment.ProcessPath)
                ? null
                : Icon.ExtractAssociatedIcon(Environment.ProcessPath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or System.ComponentModel.Win32Exception)
        {
            // 连 exe 图标都读不到：托盘会显示一个空白方块，但菜单还能用。
            return null;
        }
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = DarkMenuRenderer.Background,
            ForeColor = DarkMenuRenderer.Text,
            ShowImageMargin = false,
            // 勾选框占一列；「开机自启」的勾画在这一列里。
            ShowCheckMargin = true,
            Padding = new Forms.Padding(0, 4, 0, 4),
        };

        var open = new Forms.ToolStripMenuItem("打开主界面");
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);

        var autoStart = new Forms.ToolStripMenuItem("开机自启");
        autoStart.Click += (_, _) => ToggleAutoStart(autoStart);

        var settings = new Forms.ToolStripMenuItem("设置…");
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var exit = new Forms.ToolStripMenuItem("退出软件");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        // 每次弹菜单都重新读一遍注册表：设置窗口、别的程序、组策略都可能改过它，
        // 缓存住的勾迟早会跟真实状态对不上。
        menu.Opening += (_, _) => autoStart.Checked = AutoStart.IsEnabled;

        menu.Items.Add(open);
        menu.Items.Add(autoStart);
        menu.Items.Add(settings);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    /// <summary>
    /// 勾 / 取消开机自启。写注册表可能失败（被策略锁了、权限不够），
    /// 失败时把勾拨回真实状态，再用气泡说一句，别让界面撒谎。
    /// </summary>
    private void ToggleAutoStart(Forms.ToolStripMenuItem item)
    {
        var enable = !AutoStart.IsEnabled;
        if (AutoStart.SetEnabled(enable, out var error))
        {
            item.Checked = enable;
            return;
        }

        item.Checked = AutoStart.IsEnabled;
        ShowHint("开机自启", "没设置成功：" + (error ?? "原因不明"));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // 先摘图标再 Dispose：直接 Dispose 偶尔会在托盘里留一个死图标，要等鼠标划过才消失。
        _icon.Visible = false;
        _icon.Dispose();
        _iconStream?.Dispose();
    }

    /// <summary>
    /// 深色右键菜单。默认的 Professional 渲染是系统浅色，跟主界面对不上，
    /// 所以背景、悬停、文字、分隔线都自己画。
    /// </summary>
    private sealed class DarkMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public static readonly Color Background = Color.FromArgb(0x1A, 0x1A, 0x1A);
        public static readonly Color Text = Color.FromArgb(0xF0, 0xF0, 0xF0);
        private static readonly Color Accent = Color.FromArgb(0x4C, 0xC2, 0xFF);
        private static readonly Color Hover = Color.FromArgb(0x2E, 0x2E, 0x2E);
        private static readonly Color Separator = Color.FromArgb(0x3A, 0x3A, 0x3A);
        private static readonly Color Border = Color.FromArgb(0x3A, 0x3A, 0x3A);

        /// <summary>勾用字体画：默认那枚勾是深色位图，放在深底上等于没有。</summary>
        private static readonly Font CheckFont = new("Segoe UI Symbol", 9F, System.Drawing.FontStyle.Bold);

        public DarkMenuRenderer()
            : base(new DarkColorTable())
        {
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Background);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            e.Graphics.DrawRectangle(
                pen,
                0,
                0,
                Math.Max(0, e.ToolStrip.Width - 1),
                Math.Max(0, e.ToolStrip.Height - 1));
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled)
            {
                return;
            }

            using var brush = new SolidBrush(Hover);
            e.Graphics.FillRectangle(brush, 2, 1, Math.Max(0, e.Item.Width - 4), Math.Max(0, e.Item.Height - 2));
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled
                ? Text
                : Color.FromArgb(0x80, Text.R, Text.G, Text.B);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Separator);
            e.Graphics.DrawLine(pen, 10, y, Math.Max(10, e.Item.Width - 10), y);
        }

        /// <summary>勾选框：底色交给背景，勾自己画成亮蓝色，深底上才看得见。</summary>
        protected override void OnRenderItemCheck(Forms.ToolStripItemImageRenderEventArgs e)
        {
            var bounds = e.ImageRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = new Rectangle(4, 0, 22, e.Item.Height);
            }

            Forms.TextRenderer.DrawText(
                e.Graphics,
                "✓",
                CheckFont,
                bounds,
                Accent,
                Forms.TextFormatFlags.HorizontalCenter
                    | Forms.TextFormatFlags.VerticalCenter
                    | Forms.TextFormatFlags.NoPadding);
        }

        private sealed class DarkColorTable : Forms.ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Background;

            public override Color MenuItemSelected => Hover;

            public override Color MenuItemBorder => Hover;

            public override Color MenuBorder => Border;

            public override Color ImageMarginGradientBegin => Background;

            public override Color ImageMarginGradientMiddle => Background;

            public override Color ImageMarginGradientEnd => Background;

            public override Color CheckBackground => Background;

            public override Color CheckSelectedBackground => Hover;

            public override Color CheckPressedBackground => Hover;

            public override Color MenuItemSelectedGradientBegin => Hover;

            public override Color MenuItemSelectedGradientEnd => Hover;

            public override Color MenuItemPressedGradientBegin => Hover;

            public override Color MenuItemPressedGradientEnd => Hover;

            public override Color SeparatorDark => Separator;

            public override Color SeparatorLight => Separator;
        }
    }
}
