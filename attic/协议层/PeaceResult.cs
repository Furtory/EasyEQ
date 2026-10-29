namespace PeaceWpf.Core.Protocol;

/// <summary>
/// Peace 的返回值语义。
///
/// 注意一个真实的协议缺陷：ActionDone 把"错误码"和"数据"放在同一个整数里返回。
/// 负数既是 -1..-11 的错误码，也是合法的负 dB 值，调用方无法只凭数值区分。
/// 处理办法：
///   1. 只在"写"操作上把负数当错误；
///   2. "读"操作先确认后端在正常状态（Interface 查状态 = 1），之后把负值按数据处理；
///   3. 数据做业务范围校验（增益 -30..30 dB 之类），越界再报错。
/// </summary>
public readonly record struct PeaceResult(int Code)
{
    /// <summary>大于 0 表示动作完成。具体数值的含义随命令不同。</summary>
    public bool Ok => Code > 0;

    /// <summary>等于 2 通常是"状态已经是目标值"，但读启用状态时 2 表示"停用"。</summary>
    public bool AlreadyDone => Code == 2;

    /// <summary>负数在写操作里表示失败。</summary>
    public bool Failed => Code < 0;

    public string Describe() => Code switch
    {
        1 => "成功",
        2 => "状态已是目标值",
        -1 => "后端主循环未运行",
        -2 => "后端正停留在设置界面",
        -3 => "消息类型不匹配",
        -4 => "上一条遥控请求还没处理完",
        -5 => "动作编号不认识",
        -6 => "目标不存在（位置或组合编号越界）",
        -7 => "参数缺失或文件不存在",
        -8 => "缺少名称或长度",
        -9 => "取不到峰值通道",
        -10 => "给出的通道不在范围内",
        -11 => "拿不到计量通道数量",
        _ => $"返回值 {Code}",
    };

    public override string ToString() => $"{Code} ({Describe()})";
}

/// <summary>与 Peace 后端通信失败时抛出。</summary>
public sealed class PeaceBridgeException : Exception
{
    public PeaceBridgeException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}
