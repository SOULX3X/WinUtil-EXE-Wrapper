using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WinUtilWrapper;

/// <summary>
/// Fetches the latest winutil.ps1 from the upstream GitHub release,
/// caches it, and re-downloads only when a newer tag is detected.
/// </summary>
internal static class Updater
{
    private static readonly string CacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinUtilWrapper");

    private static readonly string ScriptPath = Path.Combine(CacheDir, "winutil.ps1");
    private static readonly string VersionStampPath = Path.Combine(CacheDir, "version.txt");
    private static readonly string LastCheckPath = Path.Combine(CacheDir, "lastcheck.txt");

    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/ChrisTitusTech/winutil/releases/latest";

    private const string LatestDownloadUrl =
        "https://github.com/ChrisTitusTech/winutil/releases/latest/download/winutil.ps1";

    /// <summary>
    /// How long to trust a cached version before re-hitting the API.
    /// Avoids GitHub rate limits when the user launches repeatedly.
    /// </summary>
    private static readonly TimeSpan VersionCheckTtl = TimeSpan.FromHours(1);

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    static Updater()
    {
        Http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("WinUtilWrapper", "1.0"));
    }

    public readonly record struct UpdateResult(
        string ScriptPath,   // full path to the ready-to-run script
        bool Updated,        // true if we just downloaded a new copy
        string Version);     // tag name (e.g. "24.07.24") or "unknown"

    /// <summary>
    /// Ensures a fresh winutil.ps1 is available locally.
    /// Rate-limits version checks to once per <see cref="VersionCheckTtl"/>.
    /// </summary>
    public static async Task<UpdateResult> EnsureLatestAsync(
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(CacheDir);

        bool shouldCheck = ShouldCheckRemote();
        if (!shouldCheck && File.Exists(ScriptPath))
        {
            var cachedVer = ReadCachedVersion();
            progress?.Report($"Using cached winutil {cachedVer}");
            return new UpdateResult(ScriptPath, false, cachedVer);
        }

        progress?.Report("Checking for winutil updates...");

        string remoteVersion;
        try
        {
            remoteVersion = await FetchLatestTagAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            remoteVersion = "unknown";
        }

        WriteLastCheckStamp();

        string cachedVersion = ReadCachedVersion();
        bool hasScript = File.Exists(ScriptPath);

        if (remoteVersion == "unknown" && hasScript)
        {
            progress?.Report($"Network check failed; using cached {cachedVersion}");
            return new UpdateResult(ScriptPath, false, cachedVersion);
        }

        // If versions match and script exists, nothing to do.
        if (remoteVersion != "unknown" && remoteVersion == cachedVersion && hasScript)
        {
            progress?.Report($"Already up-to-date ({cachedVersion})");
            return new UpdateResult(ScriptPath, false, cachedVersion);
        }

        // Download the latest bundled script.
        progress?.Report($"Downloading winutil {remoteVersion}...");
        try
        {
            var script = await Http.GetStringAsync(LatestDownloadUrl, ct).ConfigureAwait(false);

            // Write script first, then version stamp — so we never say "v2.0"
            // while actually having v1.0 on disk if the write fails mid-way.
            await File.WriteAllTextAsync(ScriptPath, script, ct).ConfigureAwait(false);
            await File.WriteAllTextAsync(VersionStampPath, remoteVersion, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ct.IsCancellationRequested is false)
        {
            if (hasScript)
            {
                progress?.Report($"Download failed: {ex.Message}; using cached {cachedVersion}");
                return new UpdateResult(ScriptPath, false, cachedVersion);
            }
            throw new InvalidOperationException(
                "Could not download winutil.ps1 and no cached copy exists.", ex);
        }

        return new UpdateResult(ScriptPath, true, remoteVersion);
    }

    // ---- private helpers -------------------------------------------------

    private static async Task<string> FetchLatestTagAsync(CancellationToken ct)
    {
        using var resp = await Http.GetAsync(LatestReleaseApiUrl, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("tag_name").GetString()
               ?? throw new InvalidOperationException("tag_name missing");
    }

    private static bool ShouldCheckRemote()
    {
        if (!File.Exists(LastCheckPath)) return true;
        try
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(LastCheckPath);
            return age > VersionCheckTtl;
        }
        catch
        {
            return true;
        }
    }

    private static void WriteLastCheckStamp()
    {
        try { File.WriteAllText(LastCheckPath, DateTime.UtcNow.ToString("O")); }
        catch { /* best-effort */ }
    }

    private static string ReadCachedVersion()
    {
        try
        {
            if (File.Exists(VersionStampPath))
                return File.ReadAllText(VersionStampPath).Trim();
        }
        catch { }
        return "unknown";
    }

    // ---- PowerShell selection --------------------------------------------

    /// <summary>
    /// Picks the best PowerShell: pwsh 7+ if installed, otherwise Windows PowerShell 5.
    /// </summary>
    public static string PickPowerShell()
    {
        // Check well-known pwsh location first (fast, no process spawn).
        var pwsh = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell", "7", "pwsh.exe");
        if (File.Exists(pwsh))
            return pwsh;

        // Probe PATH.
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "pwsh.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            proc!.WaitForExit(3000);
            var line = proc.StandardOutput.ReadLine();
            if (!string.IsNullOrWhiteSpace(line) && File.Exists(line.Trim()))
                return line.Trim();
        }
        catch { }

        return "powershell.exe";
    }
}