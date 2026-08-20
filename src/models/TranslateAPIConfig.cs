using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace LiveCaptionsTranslator.models
{
    public class TranslateAPIConfig : INotifyPropertyChanged
    {
        [JsonIgnore]
        public static Dictionary<string, string> SupportedLanguages => new()
        {
            { "zh-CN", "zh-CN" }, { "zh-TW", "zh-TW" },
            { "en-US", "en-US" }, { "en-GB", "en-GB" },
            { "ja-JP", "ja-JP" }, { "ko-KR", "ko-KR" },
            { "fr-FR", "fr-FR" }, { "th-TH", "th-TH" },
            { "ru-RU", "ru-RU" }, { "es-ES", "es-ES" },
            { "pt-BR", "pt-BR" }, { "tr-TR", "tr-TR" },
            { "ar-SA", "ar-SA" },
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            Translator.Setting?.Save();
        }
    }

    public class BaseLLMConfig : TranslateAPIConfig
    {
        public class Message
        {
            public string role { get; set; } = string.Empty;
            public string content { get; set; } = string.Empty;
        }

        private string modelName = "";
        private double temperature = 1.0;

        public string ModelName
        {
            get => modelName;
            set { modelName = value; OnPropertyChanged(); }
        }

        public double Temperature
        {
            get => temperature;
            set { temperature = value; OnPropertyChanged(); }
        }
    }

    public class OllamaConfig : BaseLLMConfig
    {
        public class Response
        {
            public string model { get; set; } = string.Empty;
            public DateTime created_at { get; set; }
            public Message message { get; set; } = new();
            public bool done { get; set; }
            public long total_duration { get; set; }
            public int load_duration { get; set; }
            public int prompt_eval_count { get; set; }
            public long prompt_eval_duration { get; set; }
            public int eval_count { get; set; }
            public long eval_duration { get; set; }
        }

        private string apiUrl = "http://localhost:11434";
        private int keep_alive = 600;

        public int Keep_alive
        {
            get => keep_alive;
            set { keep_alive = value; OnPropertyChanged(); }
        }

        public string ApiUrl
        {
            get => apiUrl;
            set { apiUrl = value; OnPropertyChanged(); }
        }
    }
}
