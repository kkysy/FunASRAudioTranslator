using System.Net.Http;
using System.Text.Json;

using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.apis
{
    public static class ModelsApiService
    {
        private static readonly HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static string GetModelsEndpoint(string apiName, string baseUrl)
        {
            return apiName == "Ollama"
                ? TextUtil.NormalizeUrl(baseUrl) + "/api/tags"
                : string.Empty;
        }

        public static async Task<List<ModelInfo>> FetchModelsAsync(
            string apiName, string baseUrl, CancellationToken token = default)
        {
            string endpoint = GetModelsEndpoint(apiName, baseUrl);
            if (string.IsNullOrEmpty(endpoint))
                return [];

            try
            {
                using var response = await client.GetAsync(endpoint, token);
                if (!response.IsSuccessStatusCode)
                    return [];

                string json = await response.Content.ReadAsStringAsync(token);
                return ParseOllamaModels(json);
            }
            catch
            {
                return [];
            }
        }

        public class ModelInfo
        {
            public string Id { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        private static List<ModelInfo> ParseOllamaModels(string json)
        {
            var result = new List<ModelInfo>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("models", out var modelsArray))
                    return result;

                foreach (var model in modelsArray.EnumerateArray())
                {
                    if (!model.TryGetProperty("name", out var nameProp))
                        continue;

                    string? name = nameProp.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        result.Add(new ModelInfo { Id = name, DisplayName = name });
                }
            }
            catch (JsonException)
            {
            }

            return result;
        }
    }
}
