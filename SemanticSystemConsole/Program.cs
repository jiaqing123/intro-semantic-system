using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticSystem.Services;

var options = new AiApiOptions();

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: options.ModelId,
    apiKey: options.ApiKey,
    endpoint: new Uri(options.BaseUrl));
var kernel = builder.Build();

// How to debug
// 1. In container window, start terminal from the running container
// 2. In the terminal, run: dotnet /app/bin/Debug/net10.0/SemanticSystemConsole.dll
// 3. In the container window, attach debug to the terminal

// 多轮对话的正确姿势：拿聊天服务 + 维护历史
var chat = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();

while (true)
{
    Console.Write("User: ");
    var userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput))
    {
        break;
    }

    history.AddUserMessage(userInput);
    var reply = await chat.GetChatMessageContentAsync(history, new OpenAIPromptExecutionSettings()
    {
        Temperature = 0.3679,
    });

    Console.WriteLine($"AI: {reply}");
    history.AddAssistantMessage(reply.ToString());   // 关键：把 AI 的回答也放回历史
}


