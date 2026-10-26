using System.Reflection;

namespace PeaceWpf;

/// <summary>
/// 关于面板要的固定信息：版本号、作者主页。不依赖任何 UI，测试时也能直接读。
/// </summary>
public static class AppInfo
{
    public const string BilibiliUrl = "https://space.bilibili.com/52593606";

    public const string GitHubUrl = "https://github.com/Furtory";

    /// <summary>当前版本号，例如 <c>1.0.0</c>。优先用 csproj 里的 Version。</summary>
    public static string Version
    {
        get
        {
            var assembly = typeof(AppInfo).Assembly;

            // .NET 会把源码版本信息拼在后面（1.0.0+abc123），只留版本段。
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                return informational.Split('+')[0];
            }

            return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        }
    }
}
