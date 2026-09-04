namespace SemanticSystem.Services
{
    public class AiApiOptions
    {
        public string BaseUrl { get; set; } = "https://api.deepseek.com";

        public string ApiKey { get; set; } = Environment.GetEnvironmentVariable("DS_API_INTRO_AI") ?? string.Empty;

        public string ModelId { get; set; } = "deepseek-v4-flash";
    }
}
