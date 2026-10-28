namespace PeaceWpf;

/// <summary>每一步要落在哪个窗口里。Main 是主窗口，其余是教程临时打开的功能窗口。</summary>
public enum TutorialWindowKind
{
    Main,
    Settings,
    PresetEq,
    AutoCalibrate,
    CalibrationTarget,
}

/// <summary>气泡相对目标控件的位置。</summary>
public enum TutorialBubblePlacement
{
    Auto,
    Top,
    Bottom,
    Left,
    Right,
    TopLeft,
    TopRight,
}

/// <summary>一条气泡引导：指哪个控件、说什么、在哪个窗口、要不要播对比。</summary>
public sealed record TutorialStep(
    TutorialWindowKind Window,
    string TargetId,
    string Title,
    string Body,
    TutorialBubblePlacement Placement,
    bool AudioDemo = false,
    bool CurveDemo = false,
    bool RealCurveDemo = false,
    bool AudioCurveDemo = false,
    bool BassBoostDemo = false,
    bool EditCurveDemo = false,
    bool LayerCompareDemo = false,
    bool LowSweepDemo = false,
    bool PreampDemo = false,
    bool BassCurvePreview = false,
    bool FlavorDemo = false,
    bool CrossoverDemo = false,
    bool BoostDemo = false);

/// <summary>
/// 两套课程的步骤表。基础课会依次打开预设EQ、自动EQ 两个功能窗口，
/// 气泡跟进去继续指窗口里的控件；进阶课都在主窗口里完成。
/// </summary>
public static class TutorialSteps
{
    private static readonly TutorialStep[] Basic =
    {
        new(TutorialWindowKind.Main, "AutoEqButton", "预设EQ",
            "按耳机型号套一份现成校正，只替换左右层。", TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.PresetEq, "AutoEqSearchBox", "搜型号",
            "数据库保持默认就行，直接搜品牌 + 型号（空格表示都要命中），下面会列出匹配的耳机。",
            TutorialBubblePlacement.Bottom),

        new(TutorialWindowKind.Main, "AutoCalibrateButton", "自动EQ",
            "扫频 + 麦克风，测你自己的耳机或房间。", TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.CalibrationTarget, "CalibrateFlatButton", "平直曲线",
            "想让每个频率一样响（音箱、房间、或者先听没有染色的声音），选这条。",
            TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.CalibrationTarget, "CalibrateHarmanButton", "哈曼曲线",
            "入耳式耳机常用的目标：低频多 6 dB 左右，中高音有点起伏。内置的是平滑近似。",
            TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.AutoCalibrate, "CalibrateDeviceBox", "选麦克风",
            "教程先按平直演示；平时可以选哈曼。测量麦最好，耳机麦两端别太当真。", TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.AutoCalibrate, "CalibrateSpeedBox", "选测试模式",
            "快速一轮约 30 秒、标准两轮约 2 分钟、更准三轮约 5 分钟；先选好档位，再开始测量。",
            TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.AutoCalibrate, "CalibratePlot", "自定义矫正起点",
            "图上那条黄色虚线就是矫正起点：往上交给「左右」层，往下交给低音层。"
            + "按住它左右拖，两层会按同一份实测重算，不用重测。",
            TutorialBubblePlacement.TopRight, CrossoverDemo: true),
        new(TutorialWindowKind.AutoCalibrate, "CalibrateBoostSlider", "科学矫正",
            "左下角这条滑杆是「补得准」和「留音量」的取舍：往左贴合目标，但整条曲线连着音量一起压下去；"
            + "拉到中间只切不补，音量最满、不削波；再往右把提升放回来，超过 0 dBFS 的部分就削掉了。",
            TutorialBubblePlacement.Top, BoostDemo: true),
        new(TutorialWindowKind.AutoCalibrate, "CalibrateStartButton", "开始测量",
            "音量调到平时位置，点下去等二十多秒到几分钟。", TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.AutoCalibrate, "CalibrateApplyButton", "套用",
            "套到「左右」层；回主界面试听再保存。", TutorialBubblePlacement.Top),
    };

    private static readonly TutorialStep[] Advanced =
    {
        new(TutorialWindowKind.Main, "MainPresetCombo", "切换预设",
            "在下拉菜单选择你需要的预设，按住右边末端拖动改变顺序，右键菜单可以执行更多操作。",
            TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.Main, "ModeMain", "进入左右编辑",
            "点「左右」进入左右层；教程已经替你切好，这层改的是左右声道的曲线。", TutorialBubblePlacement.Right),
        new(TutorialWindowKind.Main, "EqGraph", "自定义曲线",
            "按住曲线拖动，抓的就是频率最近的那颗推子，曲线始终经过鼠标；空白处按下就在鼠标处建点（Q 按邻居自动定）。滚轮改 Q，右键普通点删除、首尾端点归零，Ctrl+Z 撤回。下面是真实曲线上的演示。",
            TutorialBubblePlacement.TopRight, RealCurveDemo: true),
        new(TutorialWindowKind.Main, "SweepButton", "听左右曲线",
            "点「扫频」：先听没改过的平直曲线，再听刚才改过的左右曲线，走的都是软件自己的扫频。",
            TutorialBubblePlacement.Bottom, AudioDemo: true, EditCurveDemo: true),
        new(TutorialWindowKind.Main, "ModeBass", "进入低音编辑",
            "点「低音」切到低音层；教程这套低音用的就是 EsportsBass 那条曲线。",
            TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.Main, "BassLayerSwitch", "叠加低音修正",
            "勾选后低音层才合并；下一步听左右曲线单独和叠加低音层的区别。",
            TutorialBubblePlacement.Bottom),
        new(TutorialWindowKind.Main, "SweepButton", "听叠加",
            "点「扫频」：先听低频段（不叠低音），再听叠加低音层的低频段；走的就是软件自己的扫频。",
            TutorialBubblePlacement.Bottom,
            AudioDemo: true, LayerCompareDemo: true, LowSweepDemo: true),
        new(TutorialWindowKind.Main, "PreampSlider", "总音量",
            "声音太大或太小，就用这个推子调到合适的响度；它跟着「左右」这份配置走。",
            TutorialBubblePlacement.Left, PreampDemo: true),
        new(TutorialWindowKind.Main, "FlavorCombo", "风味曲线",
            "八种目标声音都在这个下拉里：选一个（比如脚步），把它的手柄在图上上下拖；右键手柄就还原成 0 dB。"
            + "风味只叠加，不改你的左右 / 低音曲线。",
            TutorialBubblePlacement.Left, FlavorDemo: true),
        new(TutorialWindowKind.Main, "AutoSwitchButton", "自动切换",
            "支持使用热键切换组合，还可以自动根据软件切换组合。",
            TutorialBubblePlacement.Bottom),
    };

    public static IReadOnlyList<TutorialStep> For(TutorialCourse course) =>
        course == TutorialCourse.Basic ? Basic : Advanced;
}
