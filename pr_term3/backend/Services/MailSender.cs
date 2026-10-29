using backend.Data;
using backend.Data.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;

namespace backend.Services;

/// <summary>Smtp__Host trống → không gửi thật, ghi file .eml vào PickupPath (dev mở bằng Outlook / Thunderbird để xem).</summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? User { get; init; }
    public string? Password { get; init; }
    public string From { get; init; } = "Homestay Booking <no-reply@homestay.local>";

    /// <summary>Thư mục ghi .eml khi chưa cấu hình SMTP (tương đối theo thư mục backend).</summary>
    public string PickupPath { get; init; } = "storage/mail";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

public interface IMailSender
{
    Task SendAsync(MimeMessage message, CancellationToken ct);
}

public sealed class SmtpMailSender(IOptions<SmtpOptions> options) : IMailSender
{
    public async Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient { Timeout = 30_000 };
        await client.ConnectAsync(o.Host!, o.Port, SecureSocketOptions.Auto, ct); // 465 SSL, 587 STARTTLS
        if (!string.IsNullOrEmpty(o.User)) await client.AuthenticateAsync(o.User, o.Password ?? "", ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}

public sealed class PickupFolderMailSender(IOptions<SmtpOptions> options, IHostEnvironment env) : IMailSender
{
    public string Folder => Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.PickupPath));

    public async Task SendAsync(MimeMessage message, CancellationToken ct)
    {
        Directory.CreateDirectory(Folder);
        var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{(message.MessageId ?? Guid.NewGuid().ToString("N")).Split('@')[0]}.eml";
        await using var file = File.Create(Path.Combine(Folder, name));
        await message.WriteToAsync(file, ct);
    }
}

/// <summary>Gửi thư trong email_outbox: lỗi → thử lại lần sau (tối đa MaxAttempts lần) rồi đánh dấu failed.</summary>
public sealed class EmailOutboxSender(AppDbContext db, IMailSender sender, IOptions<SmtpOptions> options, AppClock clock, ILogger<EmailOutboxSender> logger)
{
    public const int MaxAttempts = 5;
    public const int BatchSize = 20;

    public async Task<int> SendPendingAsync(CancellationToken ct)
    {
        var batch = await db.EmailOutbox.Where(m => m.Status == EmailStatus.Pending).OrderBy(m => m.Id).Take(BatchSize).ToListAsync(ct);
        var sent = 0;
        foreach (var mail in batch)
        {
            mail.Attempts++;
            try
            {
                await sender.SendAsync(ToMime(mail), ct);
                mail.Status = EmailStatus.Sent;
                mail.SentAt = clock.UtcNow;
                mail.LastError = null;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                mail.LastError = BookingLocks.Truncate(ex.Message);
                if (mail.Attempts >= MaxAttempts) mail.Status = EmailStatus.Failed;
                logger.LogWarning(ex, "Gửi email {Id} tới {To} lỗi (lần {Attempt})", mail.Id, mail.ToEmail, mail.Attempts);
            }
            await db.SaveChangesAsync(ct);
        }
        return sent;
    }

    private MimeMessage ToMime(EmailOutbox mail)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.Value.From));
        message.To.Add(MailboxAddress.Parse(mail.ToEmail));
        message.Subject = mail.Subject;
        message.MessageId = $"outbox-{mail.Id}-{Guid.NewGuid():N}@homestay.local";
        message.Body = new BodyBuilder { HtmlBody = mail.HtmlBody }.ToMessageBody();
        return message;
    }
}
