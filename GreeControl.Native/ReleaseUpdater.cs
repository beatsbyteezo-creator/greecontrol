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
    private static string? pendingInstallerPath;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GreeControl", Application.ProductVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task CheckForUpdateAsync(Action<string, string?, bool> updateStatus)
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

            updateStatus($"Atualização {latest} encontrada. Preparando download…", "Baixando…", true);

            if (!root.TryGetProperty("assets", out var assets))
            {
                updateStatus("O instalador desta atualização ainda não está disponível.", null, false);
                return;
            }
            var asset = assets.EnumerateArray().FirstOrDefault(item =>
                item.TryGetProperty("name", out var name) && name.GetString() == InstallerAsset);
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("browser_download_url", out var urlValue) ||
                !asset.TryGetProperty("digest", out var digestValue))
            {
                updateStatus("O instalador desta atualização ainda não está disponível.", null, false);
                return;
            }
            var digest = digestValue.GetString();
            var url = urlValue.GetString();
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                updateStatus("A atualização foi encontrada, mas não foi possível validar os dados do instalador.", null, false);
                return;
            }

            var updateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreeControl", "Updates");
            Directory.CreateDirectory(updateDirectory);
            var installerPath = Path.Combine(updateDirectory, InstallerAsset);
            using (var download = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                download.EnsureSuccessStatusCode();
                await using var source = await download.Content.ReadAsStreamAsync();
                await using var destination = File.Create(installerPath);
                var totalBytes = download.Content.Headers.ContentLength;
                var buffer = new byte[128 * 1024];
                long receivedBytes = 0;
                var lastReportedStep = -1;
                int count;
                while ((count = await source.ReadAsync(buffer)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, count));
                    receivedBytes += count;
                    if (totalBytes is > 0)
                    {
                        var percentage = (int)Math.Min(99, receivedBytes * 100 / totalBytes.Value);
                        var step = percentage / 5;
                        if (step != lastReportedStep)
                        {
                            lastReportedStep = step;
                            updateStatus($"Baixando atualização {latest}: {percentage}%", "Baixando…", true);
                        }
                    }
                }
            }

            await using (var file = File.OpenRead(installerPath))
            {
                var actual = "sha256:" + Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.ASCII.GetBytes(actual),
                        System.Text.Encoding.ASCII.GetBytes(digest.ToLowerInvariant())))
                {
                    File.Delete(installerPath);
                    updateStatus("A validação do instalador falhou. Nenhuma alteração foi feita.", null, false);
                    return;
                }
            }

            pendingInstallerPath = installerPath;
            updateStatus($"Atualização {latest} pronta para instalar.", "Instalar e reiniciar", true);
        }
        catch (Exception error)
        {
            updateStatus("Não foi possível baixar a atualização. O Gree Control continua aberto.", null, false);
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreeControl", "update.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"{DateTime.Now:O} {error.Message}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static bool InstallPending(Action<string, string?, bool> updateStatus, Action beginShutdown)
    {
        var installerPath = pendingInstallerPath;
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
        {
            updateStatus("O instalador ainda não está pronto. Aguarde o fim do download.", null, false);
            return false;
        }

        try
        {
            var updateDirectory = Path.GetDirectoryName(installerPath)!;
            var installer = new ProcessStartInfo(installerPath,
                "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
            {
                UseShellExecute = true,
                WorkingDirectory = updateDirectory
            };
            if (Process.Start(installer) is null)
            {
                updateStatus("O Windows não conseguiu iniciar o instalador. Tente novamente.", null, false);
                return false;
            }

            beginShutdown();
            return true;
        }
        catch (Exception error)
        {
            updateStatus("Não foi possível iniciar a atualização. O Gree Control continua aberto.", null, false);
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GreeControl", "update.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, $"{DateTime.Now:O} {error.Message}{Environment.NewLine}");
            }
            catch { }
            return false;
        }
    }
}
