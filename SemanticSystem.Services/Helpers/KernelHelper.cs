namespace SemanticSystem.Services.Helpers
{
    internal static class KernelHelper
    {
        public static Kernel CreateNew()
        {
            var options = new AiApiOptions();

            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: options.ModelId,
                apiKey: options.ApiKey,
                endpoint: new Uri(options.BaseUrl));

            var kernel = builder.Build();

            return kernel;
        }
    }
}
