using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.apis
{
    public static class TranslateAPI
    {
        public static readonly Dictionary<string, Func<string, CancellationToken, Task<string>>>
            TRANSLATE_FUNCTIONS = new()
        {
            { "Ollama", Ollama },
        };

        public static readonly List<string> LLM_BASED_APIS = ["Ollama"];
        public static readonly List<string> NO_CONFIG_APIS = [];

        public static Func<string, CancellationToken, Task<string>> TranslateFunction =>
            TRANSLATE_FUNCTIONS[Translator.Setting.ApiName];

        public static bool IsLLMBased => true;
        public static string Prompt => Translator.Setting.Prompt;

        private static readonly HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private static readonly HttpClient warmupClient = new()
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        public static async Task<bool> WarmUpOllamaAsync(CancellationToken token = default)
        {
            if (Translator.Setting?.ApiName != "Ollama")
                return true;

            var config = Translator.Setting["Ollama"] as OllamaConfig;
            if (config == null || string.IsNullOrWhiteSpace(config.ModelName))
                return false;

            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl + "/api/generate");
            var requestData = new
            {
                model = config.ModelName,
                keep_alive = config.Keep_alive,
                stream = false
            };

            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                using var response = await warmupClient.PostAsync(apiUrl, content, token);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public static async Task<string> Ollama(string text, CancellationToken token = default)
        {
            var config = Translator.Setting["Ollama"] as OllamaConfig;
            if (config == null || string.IsNullOrWhiteSpace(config.ModelName))
                return "[ERROR] Ollama model is not configured.";

            string language = OllamaConfig.SupportedLanguages.TryGetValue(
                Translator.Setting.TargetLanguage, out var langValue)
                ? langValue
                : Translator.Setting.TargetLanguage;
            string apiUrl = TextUtil.NormalizeUrl(config.ApiUrl + "/api/chat");

            var messages = new List<BaseLLMConfig.Message>
            {
                new() { role = "system", content = string.Format(Prompt, language) },
                new() { role = "user", content = $"🔤 {text} 🔤" }
            };

            if (Translator.Setting.ContextAware)
            {
                foreach (var entry in Translator.Caption.AwareContexts)
                {
                    string translatedText = entry.TranslatedText;
                    if (translatedText.Contains("[ERROR]") || translatedText.Contains("[WARNING]"))
                        continue;

                    translatedText = RegexPatterns.NoticePrefix().Replace(translatedText, "");
                    messages.InsertRange(1, [
                        new() { role = "user", content = $"🔤 {entry.SourceText} 🔤" },
                        new() { role = "assistant", content = translatedText }
                    ]);
                }
            }

            var requestData = new OllamaRequestData(config.ModelName, messages, config.Temperature)
            {
                keep_alive = config.Keep_alive
            };

            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(apiUrl, content, token);
                if (!response.IsSuccessStatusCode)
                    return $"[ERROR] Translation Failed: HTTP Error - {response.StatusCode}";

                string responseString = await response.Content.ReadAsStringAsync(token);
                var responseObj = JsonSerializer.Deserialize<OllamaConfig.Response>(responseString);
                string output = responseObj?.message?.content ?? string.Empty;
                return RegexPatterns.ModelThinking().Replace(output, "");
            }
            catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }
            catch (HttpRequestException ex)
            {
                return $"[ERROR] Translation Failed: {ex.Message}";
            }
        }
    }

    public class ConfigDictConverter : JsonConverter<Dictionary<string, List<TranslateAPIConfig>>>
    {
        public override Dictionary<string, List<TranslateAPIConfig>> Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected a StartObject token.");

            var configs = new Dictionary<string, List<TranslateAPIConfig>>();
            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string key = reader.GetString() ?? string.Empty;
                reader.Read();
                Type configType = Type.GetType($"LiveCaptionsTranslator.models.{key}Config")
                    ?? typeof(TranslateAPIConfig);

                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException("Expected a StartArray token.");

                var list = new List<TranslateAPIConfig>();
                reader.Read();
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    var config = JsonSerializer.Deserialize(ref reader, configType, options)
                        as TranslateAPIConfig ?? new TranslateAPIConfig();
                    list.Add(config);
                    reader.Read();
                }

                configs[key] = list;
                reader.Read();
            }

            if (reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("Expected an EndObject token.");
            return configs;
        }

        public override void Write(
            Utf8JsonWriter writer,
            Dictionary<string, List<TranslateAPIConfig>> value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var (key, configList) in value)
            {
                writer.WritePropertyName(key);
                writer.WriteStartArray();
                Type configType = Type.GetType($"LiveCaptionsTranslator.models.{key}Config")
                    ?? typeof(TranslateAPIConfig);
                foreach (var config in configList)
                    JsonSerializer.Serialize(writer, config, configType, options);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
    }
}
