using backend.Services;

namespace backend.Workers;

/// <summary>
/// Mỗi 30 giây: xếp thư nhắc nhận phòng ngày mai vào outbox rồi gửi các thư đang chờ (EmailOutboxSender).
/// Tắt bằng Workers__EmailOutbox=false (test gọi thẳng QueueRemindersAsync / SendPendingAsync).
/// </summary>
public sealed class EmailOutboxWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var reminders = await scope.ServiceProvider.GetRequiredService<EmailService>().QueueRemindersAsync(stoppingToken);
                var sent = await scope.ServiceProvider.GetRequiredService<EmailOutboxSender>().SendPendingAsync(stoppingToken);
                if (reminders > 0 || sent > 0) logger.LogInformation("Email: xếp {Reminders} thư nhắc, đã gửi {Sent} thư", reminders, sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi xử lý hàng đợi email");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
