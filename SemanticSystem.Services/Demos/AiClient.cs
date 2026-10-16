namespace SemanticSystem.Services.Demos;

public static class AiClient
{
    public static async Task<T?> CallWithRetryAsync<T>(Func<Task<T>> call, int maxRetries = 3, CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromSeconds(1);
        var result = default(T?);

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("操作已取消");
                    cancellationToken.ThrowIfCancellationRequested();
                }

                result = await call();
                break;
            }
            catch (Exception ex) when (attempt < maxRetries && IsRetryable(ex))
            {
                Console.WriteLine($"[重试 {attempt}/{maxRetries}] {ex.Message}，{delay.TotalSeconds}s 后重试");
                await Task.Delay(delay);
                delay *= 2;   // 指数退避：1s → 2s → 4s
            }
        }

        return result;
    }

    static bool IsRetryable(Exception ex) =>
        ex is HttpRequestException
        || ex.Message.Contains("429")   // 限流
        || ex.Message.Contains("500")    // 服务端错误
        || ex.Message.Contains("502")
        || ex.Message.Contains("503");
}

