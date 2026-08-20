using Microsoft.SemanticKernel;
using SemanticSystemConsole;

var options = new AiApiOptions();

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: options.ModelId,
    apiKey: options.ApiKey,
    endpoint: new Uri(options.BaseUrl));
var kernel = builder.Build();

var reply = await kernel.InvokePromptAsync("introduce yourself in one sentence.");
Console.WriteLine(reply);
