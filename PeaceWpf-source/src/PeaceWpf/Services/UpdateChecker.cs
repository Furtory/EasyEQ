using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace PeaceWpf;

/// <summary>一次检查更新的结论。</summary>
/// <param name="HasUpdate">线上版本是不是比本机新。</param>
/// <param name="LatestVersion">线上版本号；没读到是 null。</param>
/// <param name="ReleaseUrl">发布页地址，点「去看看」时用。</param>
/// <param name="Error">失败原因；成功时是 null。</param>
public sealed record UpdateCheckResult(
    bool HasUpdate,
    string? LatestVersion,
    string ReleaseUrl,
    string? Error);

/// <summary>
/// 检查更新：读 GitHub 的 latest release，和本机版本比。
/// 仓库还没建/没发版时返回「还没有线上版本」，不把它当错误。
/// </summary>
public static class UpdateChecker
{
    private const string Owner = "Furtory";
    private const string Repo = "EasyEQ";

    public const string ReleasesPage = $"https://github.com/{Owner}/{Repo}/releases";

    private const string LatestReleaseApi = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public static async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http
                .GetAsync(LatestReleaseApi, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                return new UpdateCheckResult(false, null, ReleasesPage, "还没有线上版本。");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, null, ReleasesPage,
                    $"更新服务器返回 {(int)response.StatusCode}，稍后再试。");
            }

            var json = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var url = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;
            var latest = NormalizeVersion(tag);

            if (latest is null)
            {
                return new UpdateCheckResult(false, null, url ?? ReleasesPage, "没读懂线上版本号。");
            }

            return new UpdateCheckResult(
                HasUpdate: Compare(latest, currentVersion) > 0,
                LatestVersion: latest,
                ReleaseUrl: url ?? ReleasesPage,
                Error: null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new UpdateCheckResult(false, null, ReleasesPage, "连不上更新服务器：" + ex.Message);
        }
    }

    /// <summary>把 <c>v1.2.3</c>、<c>1.2.3</c>、<c>release-1.2.3</c> 统一成 <c>1.2.3</c>。</summary>
    private static string? NormalizeVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var digits = new string(tag.SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => c is '.' || char.IsDigit(c)).ToArray());
        digits = digits.Trim('.', ' ');

        return digits.Length == 0 ? null : digits;
    }

    /// <summary>比较两段版本号；线上更新返回正数。解析不了的一律当相等。</summary>
    private static int Compare(string latest, string current)
    {
        if (!Version.TryParse(Normalize(latest), out var newer) || !Version.TryParse(Normalize(current), out var local))
        {
            return 0;
        }

        return newer.CompareTo(local);
    }

    private static string Normalize(string version)
    {
        var parts = version.Split('.').Take(4).ToList();
        while (parts.Count < 2)
        {
            parts.Add("0");
        }

        return string.Join('.', parts.Select(p => int.TryParse(p, out var n) ? n : 0));
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // GitHub 要求带 User-Agent，不带会直接 403。
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PeaceWpf-Settings/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
