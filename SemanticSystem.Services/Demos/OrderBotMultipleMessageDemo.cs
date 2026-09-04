using Microsoft.SemanticKernel.ChatCompletion;

namespace SemanticSystem.Services.Demos
{
    public static class OrderBotMultipleMessageDemo
    {
        /// <summary>
        ///  dotnet /app/bin/Debug/net10.0/ToolDemo.dll  
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var kernel = KernelHelper.CreateNew();

            // 模拟订单数据（内存字典）
            var orders = new Dictionary<string, string>
            {
                ["A1001"] = "无线耳机，已发货，预计 8/25 送达",
                ["A1002"] = "机械键盘，待发货"
            };

            // ① 两个"傻"工具：只认明确入参，不做任何上下文判断
            kernel.ImportPluginFromFunctions("OrderTools",
            [
                KernelFunctionFactory.CreateFromMethod(
                    (int userId) =>
                    {
                        // 模拟：userId 42 最近一笔订单是 A1001
                        Console.WriteLine($"[工具] GetRecentOrder(userId={userId})");
                        return userId == 42 ? "A1001" : "未找到该用户的订单";
                    },
                    functionName: "GetRecentOrder",
                    description: "根据用户ID查询其最近一笔订单的订单号"),

                KernelFunctionFactory.CreateFromMethod(
                    (string orderId) =>
                    {
                        Console.WriteLine($"[工具] GetOrderStatus(orderId={orderId})");
                        return orders.TryGetValue(orderId, out var s) ? s : $"订单 {orderId} 不存在，请核实订单号";
                    },
                    functionName: "GetOrderStatus",
                    description: "根据订单号查询物流状态")
            ]);

            // low, high, max
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.368,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                ReasoningEffort = "high",
            };

            // ② 多轮版：不一次性给 userId，让模型自己向用户索要。
            //    关键：直接调 IChatCompletionService 时【必须把 kernel 传进去】——
            //    否则 SK 无法从 kernel 上枚举插件，请求体里根本不会带 OrderTools，
            //    模型会以为自己是"没有工具/没有后台"的纯聊天助手。
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();

            // ③ 用户一句自然语言 → AI 先问 userId → 用户回答后再调工具
            var initInput = "请直接帮我查我刚买的耳机到哪了";
            history.AddUserMessage(initInput);
            var initReply = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken: cancellationToken);
            Console.WriteLine($"AI: {initReply}");
            history.AddAssistantMessage(initReply.ToString());

            while (true)
            {
                Console.Write("User: ");
                var userInput = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(userInput))
                {
                    break;
                }

                history.AddUserMessage(userInput);
                var reply = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken: cancellationToken);

                Console.WriteLine($"AI: {reply}");
                history.AddAssistantMessage(reply.ToString());   // 关键：把 AI 的回答也放回历史
            }
        }
    }
}
