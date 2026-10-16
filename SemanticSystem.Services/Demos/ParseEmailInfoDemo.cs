using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SemanticSystem.Services.Demos;

public static class ParseEmailInfoDemo
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var kernel = KernelHelper.CreateNew();

        // ② 关键技巧：把"强类型参数"注册成工具，让 AI 把数据填进参数
        kernel.ImportPluginFromFunctions("MailTools",
        [
            KernelFunctionFactory.CreateFromMethod(
                static (EmailInfo info) =>
                {
                    // AI 填进来的参数已经被反序列化为 EmailInfo，直接可用
                    Console.WriteLine(JsonSerializer.Serialize(info, jsonSerializerOptions));
                    return "已记录";
                },
                functionName: "RecordEmailInfo",
                description: "记录一封邮件的结构化信息（发件人、意图、紧急度、摘要、是否需要回复）")
        ]);

        var email = """
            发件人: bob@example.com
            主题: 退款申请
            内容: 我上周买的键盘坏了，按你们要求提交了照片，但三天没有回复。请尽快处理退款，否则我要投诉。
        """;

        await InvokePromptWithToolAsync(kernel, email, cancellationToken);

        // ③ 对比实验：再试"裸 JSON"模式（不注册工具，直接要 JSON）
        await InvokePromptWithoutToolAsync(kernel, email, cancellationToken);
    }

    private static async Task InvokePromptWithToolAsync(Kernel kernel, string email, CancellationToken cancellationToken)
    {
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            // 区别在这里：告诉 AI 自动调用注册的工具
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            ReasoningEffort = "low",
        };
        // 详细的 prompt：$"请分析下面这封邮件，并调用 RecordEmailInfo 记录分析结果：\n{email}"
        // 因为我们已经注册了 RecordEmailInfo 工具，所以 AI 会自动调用它，而不需要我们在 prompt 里显式要求调用。
        var prompt = $"请分析下面这封邮件，记录分析结果：\n{email}";
        FunctionResult reply = await kernel.InvokePromptAsync(
            prompt,
            new(settings), cancellationToken: cancellationToken);
        try
        {
            var messages = new List<string>();

            if (reply.GetValue<ChatMessageContent>() is ChatMessageContent chatMessageContent)
            {
                var contentString = chatMessageContent.Content ?? string.Empty;
                Console.WriteLine(contentString);
                messages.Add(contentString);
            }

            if (reply.GetValue<OpenAIChatMessageContent>() is OpenAIChatMessageContent openAIChatMessageContent)
            {
                var contentString = openAIChatMessageContent.Content ?? string.Empty;
                Console.WriteLine(contentString);
                messages.Add(contentString);
            }

            var replayString = reply.ToString() ?? string.Empty;
            messages.Add(replayString);

            if (messages.Distinct().Count() > 1)
            {
                Console.WriteLine("注意：不同的消息类型返回了不同的内容，可能是因为 AI 在生成 JSON 时出现了不一致。");
            }

            var obj = JsonSerializer.Deserialize<EmailInfo>(replayString);
            Console.WriteLine($"JSON模式解析成功: {obj}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"JSON模式解析失败（这就是函数调用方案存在的理由）: {ex.Message}");
            Console.WriteLine($"原始输出: {reply}");
        }
    }

    private static async Task InvokePromptWithoutToolAsync(Kernel kernel, string email, CancellationToken cancellationToken)
    {
        var rawSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0
        };
        FunctionResult rawReply = await kernel.InvokePromptAsync(
            $$"""
            分析下面邮件，只输出 JSON，不要任何其他文字，格式：
            {"Sender":"","Intent":"","Urgency":"","Summary":"","NeedsReply":true}
            邮件：{{email}}
            """, new(rawSettings), cancellationToken: cancellationToken);
        try
        {
            var messages = new List<string>();

            if (rawReply.GetValue<ChatMessageContent>() is ChatMessageContent rawChatMessageContent)
            {
                var contentString = rawChatMessageContent.Content ?? string.Empty;
                Console.WriteLine(contentString);
                messages.Add(contentString);
            }

            if (rawReply.GetValue<OpenAIChatMessageContent>() is OpenAIChatMessageContent rawOpenAIChatMessageContent)
            {
                var contentString = rawOpenAIChatMessageContent.Content ?? string.Empty;
                Console.WriteLine(contentString);
                messages.Add(contentString);
            }

            var rawReplyString = rawReply.ToString() ?? string.Empty;
            messages.Add(rawReplyString);

            if (messages.Distinct().Count() > 1)
            {
                Console.WriteLine("注意：不同的消息类型返回了不同的内容，可能是因为 AI 在生成 JSON 时出现了不一致。");
            }

            var rawObj = JsonSerializer.Deserialize<EmailInfo>(rawReplyString);
            Console.WriteLine($"裸JSON模式解析成功: {rawObj}");
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"裸JSON模式解析失败（这就是函数调用方案存在的理由）: {ex.Message}");
            Console.WriteLine($"原始输出: {rawReply}");
        }
    }
}

// ① 强类型模型（记录类型，System.Text.Json 直接支持）
public record EmailInfo(
    string Sender,      // 发件人
    string Intent,      // 意图：咨询/投诉/下单/通知...
    string Urgency,     // 紧急度：高/中/低
    string Summary,     // 一句话摘要
    bool NeedsReply);   // 是否需要人工回复
