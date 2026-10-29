namespace PeaceWpf.ViewModels;

/// <summary>
/// 顶部两个互斥按钮。默认全灭：曲线只读，不接受任何编辑。
/// 均衡（左右音量）不是模式了，滑块常驻；拖它一律先自己点亮「左右」进编辑，
/// 这样改动算草稿、可以取消，不会拖完就被当成已保存写回去。
/// </summary>
public enum MainMode
{
    /// <summary>全灭，曲线只读。</summary>
    None = 0,

    /// <summary>左右：主 EQ 曲线。</summary>
    MainLayer = 2,

    /// <summary>低音：低音 EQ 曲线，按开关决定是否与主层叠加。</summary>
    BassLayer = 3,
}

/// <summary>有脏改动时，用户对“保存还是放弃”的选择。</summary>
public enum DirtyChoice
{
    /// <summary>先保存再继续。</summary>
    Save = 0,

    /// <summary>放弃改动，回到上次保存的状态。</summary>
    Discard = 1,

    /// <summary>什么都不做，留在当前状态。</summary>
    Stay = 2,
}
