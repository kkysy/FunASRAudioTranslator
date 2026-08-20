using System.Net.Http;
using System.Text.Json;

namespace LiveCaptionsTranslator.utils
{
    public static class UpdateUtil
    {
        // Configure this before publishing a build, or leave it unset to disable update checks.
        private static string RepositoryUrl =>
            (Environment.GetEnvironmentVariable("FUN_ASR_REPOSITORY_URL")
             ?? Environment.GetEnvironmentVariable("LIVE_CAPTIONS_REPOSITORY_URL"))?.TrimEnd('/') ?? string.Empty;

        public static string GitHubRepoUrl => RepositoryUrl;
        public static string GitHubReleasesUrl => string.IsNullOrEmpty(RepositoryUrl)
            ? string.Empty
            : $"{RepositoryUrl}/releases";
        public static string GitHubLatestReleaseApi => string.IsNullOrEmpty(RepositoryUrl)
            ? string.Empty
            : $"{RepositoryUrl.Replace("https://github.com/", "https://api.github.com/repos/")}/releases/latest";

        public static async Task<string> GetLatestVersion()
        {
            string apiUrl = GitHubLatestReleaseApi;
            if (string.IsNullOrEmpty(apiUrl))
                return string.Empty;

            using var client = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FunASR-System-Audio-Translator");
            var response = await client.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);
            var latestVersionRaw = doc.RootElement.GetProperty("tag_name").GetString();
            var latestVersion = string.IsNullOrEmpty(latestVersionRaw)
                ? String.Empty
                : RegexPatterns.VersionNumber().Replace(latestVersionRaw, "");
            return latestVersion;
        }


    }
}
