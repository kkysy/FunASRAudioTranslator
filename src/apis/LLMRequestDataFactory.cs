using LiveCaptionsTranslator.models;

namespace LiveCaptionsTranslator.apis
{
    public static class LLMRequestDataFactory
    {
        public static OllamaRequestData Create(
            string model, List<BaseLLMConfig.Message> messages, double temperature)
        {
            return new OllamaRequestData(model, messages, temperature);
        }
    }
}
