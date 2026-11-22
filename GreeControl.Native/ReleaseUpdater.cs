using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace GreeControl.Native;

internal static class ReleaseUpdater
{
    private const string Repository = "beatsbyteezo-creator/GreeControl";
    private const string InstallerAsset = "GreeControl-Setup-x64.exe";
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GreeControl", Application.ProductVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task CheckAndInstallAsync(Action beginShutdown)
    {
        try
        {
            using var response = await Client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
            if (!response.IsSuccessStatusCode) return;
            using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = release.RootElement;
            if (!root.TryGetProperty("tag_name", out var tagValue) ||
                !Version.TryParse(tagValue.GetString()?.TrimStart('v', 'V'), out var latest) ||
                !Version.TryParse(Application.ProductVersion.Split('+')[0], out var current) || latest <= current)
                return;

            if (!root.TryGetProperty("assets", out var assets)) return;
            var asset = assets.EnumerateArray().FirstOrDefault(item =>
                item.TryGetProperty("name", out var name) && name.GetString() == InstallerAsset);
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("browser_download_url", out var urlValue) ||
                !asset.TryGetProperty("digest", out var digestValue)) return;
            var digest = digestValue.GetString();
            var url = urlValue.GetString();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return;

            var updateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreeControl", "Updates");
            Directory.CreateDirectory(updateDirectory);
            var installerPath = Path.Combine(updateDirectory, InstallerAsset);
            using (var download = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                download.EnsureSuccessStatusCode();
                await using var source = await download.Content.ReadAsStreamAsync();
                await using var destination = File.Create(installerPath);
                await source.CopyToAsync(destination);
            }

            await using (var file = File.OpenRead(installerPath))
            {
                var actual = "sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.ASCII.GetBytes(actual),
                        System.Text.Encoding.ASCII.GetBytes(digest.ToLowerInvariant())))
                {
                    File.Delete(installerPath);
                    return;
                }
            }

            var installer = new ProcessStartInfo(installerPath,
                "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
            {
                UseShellExecute = true,
                WorkingDirectory = updateDirectory
            };
            if (Process.Start(installer) is not null) beginShutdown();
        }
        catch (Exception error)
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreeControl", "update.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"{DateTime.Now:O} {error.Message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
