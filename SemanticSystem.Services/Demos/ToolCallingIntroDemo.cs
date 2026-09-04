namespace SemanticSystem.Services.Demos
{
    public static class ToolCallingIntroDemo
    {
        public static async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var kernel = KernelHelper.CreateNew();

            // ① 工具 = 普通 C# 方法，完全不需要关心上下文
            kernel.ImportPluginFromFunctions("MyTools",
            [
                KernelFunctionFactory.CreateFromMethod(
                    (string email) =>
                    {
                        bool ok = RegexHelper.EmailRegex().IsMatch(email);
                        return ok ? "邮箱格式合法" : "邮箱格式错误";
                    },
                    functionName: "ValidateEmail",
                    description: "验证用户输入的邮箱地址是否合法"),

                KernelFunctionFactory.CreateFromMethod(
                    (int a, int b) => a + b,
                    functionName: "Add",
                    description: "计算两个整数的和")
            ]);

            // ② 开启自动工具调用
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.7,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                ReasoningEffort = "low",
            };

            // ③ 让 AI 自己决定：这句该不该调工具、调哪个
            Console.WriteLine(await kernel.InvokePromptAsync("帮我看看 bob#com 是不是合法邮箱", new(settings), cancellationToken: cancellationToken));
            Console.WriteLine(await kernel.InvokePromptAsync("3 加 5 等于几？", new(settings), cancellationToken: cancellationToken));
            Console.WriteLine(await kernel.InvokePromptAsync("今天天气怎么样？", new(settings), cancellationToken: cancellationToken)); // 不该调工具，直接回答
        }
    }
}
