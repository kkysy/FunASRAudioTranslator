namespace LiveCaptionsTranslator.models
{
    public class OllamaRequestData(
        string model,
        List<BaseLLMConfig.Message> messages,
        double temperature)
    {
        public string model { get; set; } = model;
        public List<BaseLLMConfig.Message> messages { get; set; } = messages;
        public double temperature { get; set; } = temperature;
        public int max_tokens { get; set; } = 128;
        public bool stream { get; set; } = false;
        public int keep_alive { get; set; } = 600;
        public bool think { get; set; } = false;
    }
}
