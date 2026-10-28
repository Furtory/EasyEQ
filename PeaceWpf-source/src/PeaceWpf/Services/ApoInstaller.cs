using System.Diagnostics;
using System.IO;
using System.Net.Http;
using PeaceWpf.Core;

namespace PeaceWpf;

/// <summary>一次下载的结论。</summary>
/// <param name="Path">下载好的安装包路径；失败时是 null。</param>
/// <param name="Error">失败原因；成功时是 null。</param>
public sealed record ApoDownloadResult(string? Path, string? Error);

/// <summary>
/// EqualizerAPO 的下载与安装。
///
/// 安装包从官方 SourceForge 的 latest 链接拿；下载完以管理员身份拉起安装程序，
/// 选设备那一步必须人点，所以这里只负责把安装器送到用户面前。
/// </summary>
public static class ApoInstaller
{
    /// <summary>手动下载页，自动下载失败时退到这里。</summary>
    public const string DownloadPage = "https://sourceforge.net/projects/equalizerapo/";

    /// <summary>SourceForge 的「最新版本」直链，会重定向到具体安装包。</summary>
    private const string LatestDownload = "https://sourceforge.net/projects/equalizerapo/files/latest/download";

    private static readonly HttpClient Http = CreateClient();

    /// <summary>下载安装包到临时目录；进度 0–1。</summary>
    public static async Task<ApoDownloadResult> DownloadAsync(
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var folder = Path.Combine(Path.GetTempPath(), AppPaths.Name);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "EqualizerAPO-setup.exe");

            using var response = await Http
                .GetAsync(LatestDownload, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new ApoDownloadResult(null, $"下载服务器返回 {(int)response.StatusCode}。");
            }

            // SourceForge 偶尔把网页当结果返回，别把 HTML 存成 exe。
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is "text/html")
            {
                return new ApoDownloadResult(null, "下载页返回的是网页，不是安装包。");
            }

            var total = response.Content.Headers.ContentLength ?? -1;
            var buffer = new byte[81920];
            long received = 0;

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = File.Create(path))
            {
                int count;
                while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                    received += count;
                    if (total > 0)
                    {
                        progress?.Report((double)received / total);
                    }
                }
            }

            // exe 的头两个字节是 MZ；不是就别让用户双击一个坏文件。
            await using (var check = File.OpenRead(path))
            {
                var header = new byte[2];
                if (await check.ReadAsync(header, cancellationToken).ConfigureAwait(false) < 2
                    || header[0] != (byte)'M'
                    || header[1] != (byte)'Z')
                {
                    File.Delete(path);
                    return new ApoDownloadResult(null, "下载到的文件不是安装程序。");
                }
            }

            return new ApoDownloadResult(path, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            return new ApoDownloadResult(null, ex.Message);
        }
    }

    /// <summary>
    /// 以管理员身份拉起安装程序。用户在 UAC 里点「否」时返回 false 并说明。
    /// </summary>
    public static bool LaunchInstaller(string installerPath, out string? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Verb = "runas",
            });

            error = null;
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            error = "你在 UAC 弹窗里点了「否」，安装没有开始。";
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>打开下载页（手动兜底）。</summary>
    public static bool OpenDownloadPage(out string? error)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DownloadPage,
                UseShellExecute = true,
            });

            error = null;
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PeaceWpf-Settings/1.0");
        return client;
    }
}
