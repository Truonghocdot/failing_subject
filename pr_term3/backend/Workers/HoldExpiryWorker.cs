using backend.Services;

namespace backend.Workers;

/// <summary>
/// Mỗi phút chuyển đặt phòng chờ cọc quá hạn giữ chỗ sang "hết hạn" và nhả đêm (BookingService.ExpireDueAsync).
/// Tắt bằng Workers__HoldExpiry=false (test gọi thẳng ExpireDueAsync sau khi tua đồng hồ giả).
/// </summary>
public sealed class HoldExpiryWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<HoldExpiryWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var expired = await scope.ServiceProvider.GetRequiredService<BookingService>().ExpireDueAsync(stoppingToken);
                if (expired > 0) logger.LogInformation("Hết hạn giữ chỗ: {Count} đặt phòng, đã nhả đêm", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // DB tạm mất kết nối... → thử lại phút sau, không làm sập tiến trình
                logger.LogError(ex, "Lỗi khi xử lý hết hạn giữ chỗ");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
