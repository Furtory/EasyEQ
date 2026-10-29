using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PeaceWpf.Controls;

namespace PeaceWpf.Services;

/// <summary>
/// 覆盖整个屏幕的透明通知层：从顶部滑下一条曲线通知，停一会儿，再滑回去。
///
/// 为什么不用托盘气泡：系统气泡是 Windows 自己的样式，位置、颜色、时长都不受控，
/// 而且经常被系统通知设置直接关掉。这里用一个铺满虚拟屏幕、背景全透明的 Topmost 窗口，
/// 里面只放一条 <see cref="CurveOsdBanner"/> —— 看起来像 OSD，又不抢鼠标、不抢焦点。
///
/// 窗口只建一次，之后反复显示 / 隐藏：建一次全屏透明窗口要初始化 DWM 合成，
/// 每次最小化都新建会闪。
/// </summary>
public sealed class CurveOsdService : IDisposable
{
    private const double BannerWidth = 560;

    /// <summary>通知条控件总高：可见部分 + 伸出屏幕顶边的那一截。</summary>
    private const double BannerHeight = CurveOsdBanner.PreferredHeight + CurveOsdBanner.TopBleed;

    /// <summary>下滑 / 上收的时长；停住的时间给眼睛读完两行字。</summary>
    private const int SlideInMs = 300;
    private const int HoldMs = 2600;
    private const int SlideOutMs = 240;

    /// <summary>滑到位之后，曲线从上一套变成这一套用的时间。</summary>
    private const int MorphMs = 420;

    /// <summary>窗口扩展样式：鼠标穿透、不抢焦点、不进 Alt+Tab。</summary>
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private Window? _window;
    private CurveOsdBanner? _banner;
    private TranslateTransform? _slide;
    private DispatcherTimer? _holdTimer;

    /// <summary>连发通知时用它判断「这个计时器还是不是当前这一条」。</summary>
    private int _generation;

    private bool _disposed;

    /// <summary>
    /// 启动时调用：把全屏透明通知窗口建好并一直显示着。
    ///
    /// 窗口本身是全透明的（里面只有一根通知条），所以常驻也看不见；
    /// 好处是第一次弹通知不再需要创建分层窗口、编译阴影着色器，滑入动画不会卡在开头。
    /// 窗口已加 WS_EX_TRANSPARENT / WS_EX_NOACTIVATE：鼠标能穿透，也不会抢焦点。
    /// </summary>
    public void Start()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            EnsureWindow();
            if (!_window!.IsVisible)
            {
                _window.Show();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Debug.WriteLine($"通知窗口启动失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 显示一条通知。重复调用会把上一条直接换掉，从顶部重新滑一次。
    /// curve 为空时只显示文字，仍然会弹。
    /// </summary>
    public void Show(string title, string detail, IReadOnlyList<double>? curve, Color accent)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            EnsureWindow();

            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var width = Math.Min(BannerWidth, Math.Max(320, screenWidth - 48));
            _banner!.Width = width;
            _banner.Height = BannerHeight;

            // 先摘掉上一条的曲线动画：不然 Show 设的新起点（0）会被还没跑完的旧动画压住。
            _banner.BeginAnimation(CurveOsdBanner.MorphProgressProperty, null);
            _banner.Show(title, detail, curve, accent);

            // 滑下来落定之后再让曲线从上一套形状变到这一套，两个动作不叠在一起抢视线。
            if (_banner.IsMorphing)
            {
                var morph = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(MorphMs))
                {
                    BeginTime = TimeSpan.FromMilliseconds(SlideInMs),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.HoldEnd,
                };
                _banner.BeginAnimation(CurveOsdBanner.MorphProgressProperty, morph);
            }

            // 主屏在虚拟屏幕坐标系里的原点就是 (0,0)，通知条在主屏顶部居中。
            // 布局放在 0：落定时由滑入动画把它往 -TopBleed 挪，上面那一截背景被屏幕边缘裁掉。
            Canvas.SetLeft(_banner, (screenWidth - width) / 2);
            Canvas.SetTop(_banner, 0);

            if (!_window!.IsVisible)
            {
                _window.Show();
            }

            var generation = ++_generation;
            _holdTimer?.Stop();

            // 滑动期间不开阴影：DropShadowEffect 每帧重算，会和动画抢 GPU。
            _banner.ShowShadow(false);

            PlaySlide(
                from: -BannerHeight - 16,
                to: -CurveOsdBanner.TopBleed,
                SlideInMs,
                EasingMode.EaseOut,
                completed: () =>
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    // 落定之后再挂阴影、开始计时：停留时间按「画面上看得见的那一下」算，
                    // 不把窗口首次合成的卡顿算进去，所以停留不会被吃掉。
                    _banner.ShowShadow(true);
                    StartHold(generation);
                });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 通知只是锦上添花：弹不出来不能连累主程序（比如远程桌面会话里没有桌面合成）。
            System.Diagnostics.Debug.WriteLine($"屏幕通知显示失败：{ex.Message}");
        }
    }

    /// <summary>滑到位之后开始计停留时间，时间到了再收回去。</summary>
    private void StartHold(int generation)
    {
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(HoldMs) };
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer?.Stop();
            if (generation == _generation)
            {
                HideAnimated();
            }
        };
        _holdTimer.Start();
    }

    /// <summary>
    /// 收起来：只把通知条滑出屏幕顶边，窗口本身不隐藏 —— 它从启动起就一直挂在那儿，
    /// 下次显示不需要重建窗口，也不会闪。
    /// </summary>
    private void HideAnimated()
    {
        if (_slide is null || _window is null)
        {
            return;
        }

        _banner?.ShowShadow(false);

        PlaySlide(
            from: _slide.Y,
            to: -BannerHeight - 16,
            SlideOutMs,
            EasingMode.EaseIn);
    }

    private void PlaySlide(double from, double to, int milliseconds, EasingMode mode, Action? completed = null)
    {
        if (_slide is null)
        {
            return;
        }

        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = mode },
            FillBehavior = FillBehavior.HoldEnd,
        };

        if (completed is not null)
        {
            animation.Completed += (_, _) => completed();
        }

        _slide.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _banner = new CurveOsdBanner
        {
            Height = BannerHeight,
            IsHitTestVisible = false,
        };

        _slide = new TranslateTransform(0, -BannerHeight - 16);
        _banner.RenderTransform = _slide;

        var host = new Canvas
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false,
        };
        host.Children.Add(_banner);

        _window = new Window
        {
            Title = "曲线通知",
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = true,
            ShowActivated = false,
            Focusable = false,
            IsHitTestVisible = false,
            Left = SystemParameters.VirtualScreenLeft,
            Top = SystemParameters.VirtualScreenTop,
            Width = Math.Max(SystemParameters.VirtualScreenWidth, 200),
            Height = Math.Max(SystemParameters.VirtualScreenHeight, 200),
            Content = host,
        };

        _window.SourceInitialized += (_, _) => MakeClickThrough(_window);
    }

    /// <summary>
    /// 给窗口加上「鼠标穿透 + 不激活」的扩展样式。
    /// 全屏 Topmost 窗口如果不这么做，会吃掉整个屏幕的点击、还会把游戏顶掉。
    /// </summary>
    private static void MakeClickThrough(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, style | WsExTransparent | WsExNoActivate | WsExToolWindow);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _holdTimer?.Stop();
        _holdTimer = null;

        if (_window is not null)
        {
            _slide?.BeginAnimation(TranslateTransform.YProperty, null);
            _window.Close();
            _window = null;
        }

        _banner = null;
        _slide = null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
