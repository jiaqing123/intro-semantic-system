namespace SemanticSystem.Services.Demos
{
    public static class OrderBotDemo
    {
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

            // ② 宿主把 userId 通过模板变量 {{$userId}} 注入提示词：
            //    KernelArguments 本身模型看不到，必须渲染进对话，模型才知道 userId=42
            var args = new KernelArguments(settings) { ["userId"] = 42 };

            // ③ 用户一句自然语言 → AI 两步调用
            var reply = await kernel.InvokePromptAsync(
                "当前登录用户的 userId 是 {{$userId}}（整数）。请直接帮我查我刚买的耳机到哪了，不要向用户索要ID。",
                args, cancellationToken: cancellationToken);
            Console.WriteLine($"AI: {reply}");
        }
    }
}
