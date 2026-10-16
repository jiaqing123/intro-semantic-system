using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace SemanticSystem.Services.Demos;

public static class StreamAnswerDemo
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var kernel = KernelHelper.CreateNew();

        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage("你是一个非常聪明的AI助手(蓝色大肥鱼)，能够帮助用户解决各种问题。");

        while (!cancellationToken.IsCancellationRequested)
        {
            var promptMePrefix = "你> ";
            Console.Write(promptMePrefix);

            var input = Console.ReadLine()?[promptMePrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            history.AddUserMessage(input);

            var promptAiPrefix = "大肥鱼> ";
            Console.Write(promptAiPrefix);

            var builder = new StringBuilder();

            await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken))
            {
                Console.Write(chunk);
                builder.Append(chunk);
            }

            Console.WriteLine();

            history.AddAssistantMessage(builder.ToString());
        }
    }
}
