using System.Globalization;

namespace PeaceWpf.Core.Model;

/// <summary>界面上的数值文案。曲线浮标和状态栏共用，避免两处格式不一致。</summary>
public static class EqFormat
{
    /// <summary>20 → “20 Hz”，1200 → “1.2 kHz”。</summary>
    public static string Frequency(double hz)
    {
        if (!double.IsFinite(hz) || hz <= 0)
        {
            return "—";
        }

        return hz >= 1000
            ? string.Create(CultureInfo.InvariantCulture, $"{hz / 1000:0.##} kHz")
            : string.Create(CultureInfo.InvariantCulture, $"{hz:0.#} Hz");
    }

    /// <summary>0 → “0.0 dB”，3.5 → “+3.5 dB”。</summary>
    public static string Gain(double db) =>
        string.Create(CultureInfo.InvariantCulture, $"{db:+0.0;-0.0;0.0} dB");

    /// <summary>1.41 → “Q 1.41”。</summary>
    public static string Quality(double q) =>
        string.Create(CultureInfo.InvariantCulture, $"Q {q:0.##}");

    /// <summary>曲线浮标用的整行文案。</summary>
    public static string Describe(double hz, double db, double q) =>
        $"{Frequency(hz)}   {Gain(db)}   {Quality(q)}";
}
