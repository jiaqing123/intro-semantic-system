using Microsoft.Data.Sqlite;
using System.Text;

namespace SemanticSystem.Services.Demos;

internal static class MailClassifierDemo
{
    public static async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var kernel = KernelHelper.CreateNew();

        // ---------- ② 待分类邮件（真实项目可改成从文件/IMAP 读）----------
        var emails = new[]
        {
            "发件人: bob@example.com\n主题: 退款申请\n内容: 键盘坏了三天没人理，请尽快退款，否则投诉。",
            "发件人: alice@example.com\n主题: 产品咨询\n内容: 请问你们的机械键盘支持蓝牙吗？想了解一下再下单。",
            "发件人: spam@example.com\n主题: 恭喜中奖\n内容: 恭喜您获得 iPhone 大奖，点击链接领取！"
        };

        // ---------- ③ 注册分类工具（D5 的技巧：用强类型参数让 AI 填）----------
        EmailInfo? captured = null;
        kernel.ImportPluginFromFunctions("MailTools",
        [
            KernelFunctionFactory.CreateFromMethod(
                (EmailInfo info) => { captured = info; return "已记录"; },
                functionName: "ClassifyEmail",
                description: "记录邮件分类结果（发件人、意图、紧急度、摘要、是否需要回复）")
        ]);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        // ---------- ④ 建库建表 ----------
        using var conn = new SqliteConnection("Data Source=mails.db");
        await conn.OpenAsync(cancellationToken);
        using var cmd = new SqliteCommand("""
        CREATE TABLE IF NOT EXISTS Emails(
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            Sender     TEXT NOT NULL,
            Intent     TEXT NOT NULL,
            Urgency    TEXT NOT NULL,
            Summary    TEXT NOT NULL,
            NeedsReply INTEGER NOT NULL,
            CreatedAt  TEXT NOT NULL)
        """, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // ---------- ⑤ 逐封分类 + 入库（合体的地方）----------
        foreach (var email in emails)
        {
            EmailInfo? info;
            try
            {
                // ⭐ D6 的 AiClient 包住 D5 的工具调用，实现首包重试
                info = await AiClient.CallWithRetryAsync(async () =>
                {
                    captured = null;   // 每次尝试前重置，避免重试残留上次的半成品
                    await kernel.InvokePromptAsync(
                        $"请分析下面这封邮件，调用 ClassifyEmail 记录结果：\n{email}",
                        new(settings));
                    return captured
                        ?? throw new InvalidOperationException("AI 没有调用 ClassifyEmail");
                }, maxRetries: 3, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[跳过] 分类失败：{ex.Message}");
                continue;
            }

            using var insert = new SqliteCommand("""
            INSERT INTO Emails(Sender, Intent, Urgency, Summary, NeedsReply, CreatedAt)
            VALUES (@s, @i, @u, @sum, @n, @t)
            """, conn);
            insert.Parameters.AddWithValue("@s", info.Sender);
            insert.Parameters.AddWithValue("@i", info.Intent);
            insert.Parameters.AddWithValue("@u", info.Urgency);
            insert.Parameters.AddWithValue("@sum", info.Summary);
            insert.Parameters.AddWithValue("@n", info.NeedsReply ? 1 : 0);
            insert.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            insert.ExecuteNonQuery();

            Console.WriteLine($"[入库] {info.Sender} | {info.Urgency} | {info.Summary}");
        }
    }
}
