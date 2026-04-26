// AeroDial — UpdateChecker.cs
// Queries the GitHub Releases API to check whether a newer version is available.
// Uses a plain HttpClient with a 10-second timeout so it never blocks the UI.

using System.Net.Http;
using System.Text.Json;

namespace AeroDial.Core;

internal static class UpdateChecker
{
    public enum UpdateStatus { Unknown, UpToDate, UpdateAvailable, Error }

    /// <summary>
    /// Checks GitHub for a newer release.
    /// Returns (status, latestVersion, releasePageUrl).
    /// </summary>
    public static async Task<(UpdateStatus Status, string? LatestVersion, string? ReleaseUrl)> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", $"{AppConstants.AppName}/{AppConstants.Version}");
            http.Timeout = TimeSpan.FromSeconds(10);

            var json = await http.GetStringAsync(AppConstants.GitHubReleasesApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagEl))
                return (UpdateStatus.Error, null, null);

            string tag        = tagEl.GetString() ?? "";
            string releaseUrl = root.TryGetProperty("html_url", out var urlEl)
                ? (urlEl.GetString() ?? AppConstants.GitHubUrl) : AppConstants.GitHubUrl;

            // Strip leading 'v' prefix before comparing (v1.0.0 → 1.0.0)
            string latestClean  = tag.TrimStart('v');
            string currentClean = AppConstants.Version.TrimStart('v');

            bool isNewer = IsNewerVersion(latestClean, currentClean);
            return isNewer
                ? (UpdateStatus.UpdateAvailable, latestClean, releaseUrl)
                : (UpdateStatus.UpToDate,        latestClean, releaseUrl);
        }
        catch (Exception ex)
        {
            Logger.Warn("Update check failed", ex);
            return (UpdateStatus.Error, null, null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsNewerVersion(string latest, string current)
    {
        if (Version.TryParse(latest, out var lv) && Version.TryParse(current, out var cv))
            return lv > cv;
        // Fallback: lexicographic comparison (handles pre-release tags like "1.1.0-beta")
        return string.CompareOrdinal(latest, current) > 0;
    }
}
