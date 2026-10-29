using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Services;

public enum EmailKind
{
    Created,
    Confirmed,
    Cancelled,
    Reminder,
}

/// <summary>
/// Thư báo cho khách (docs/02-nghiep-vu.md §12) – chỉ ghi vào email_outbox trong cùng transaction với thay đổi đặt phòng
/// (outbox: không gửi thư cho thay đổi bị rollback); EmailOutboxWorker gửi sau. Khách không có email → bỏ qua.
/// </summary>
public sealed class EmailService(AppDbContext db, AppClock clock, IOptions<AppOptions> app, IOptions<PaymentOptions> bank)
{
    public const string ReminderEvent = "reminder";

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    // Chỉ mã hoá ký tự HTML đặc biệt – giữ nguyên chữ có dấu (WebUtility.HtmlEncode đổi "á" thành "&#225;")
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>Thêm thư vào outbox (chưa SaveChanges). h / r = homestay, phòng của đặt phòng; b.Payments phải đã nạp.</summary>
    public void Queue(Booking b, Homestay h, Room r, EmailKind kind)
    {
        if (string.IsNullOrWhiteSpace(b.GuestEmail)) return;
        var (subject, html) = Render(b, h, r, kind);
        db.EmailOutbox.Add(new EmailOutbox { ToEmail = b.GuestEmail, Subject = BookingLocks.Truncate(subject, 200), HtmlBody = html });
    }

    /// <summary>Nhắc nhận phòng: đặt phòng đã xác nhận, nhận phòng ngày mai, chưa nhắc (event "reminder") – chạy lại không gửi trùng.</summary>
    public async Task<int> QueueRemindersAsync(CancellationToken ct)
    {
        var tomorrow = clock.Today.AddDays(1);
        var due = await db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.CheckIn == tomorrow && b.GuestEmail != null
                        && !db.BookingEvents.Any(e => e.BookingId == b.Id && e.Type == ReminderEvent))
            .Include(b => b.Homestay).Include(b => b.Room).Include(b => b.Payments)
            .OrderBy(b => b.Id).Take(100)
            .ToListAsync(ct);
        foreach (var b in due)
        {
            Queue(b, b.Homestay, b.Room, EmailKind.Reminder);
            db.Log(b, ReminderEvent, $"Gửi email nhắc nhận phòng tới {b.GuestEmail}", null);
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }

    private (string Subject, string Html) Render(Booking b, Homestay h, Room r, EmailKind kind)
    {
        var money = PaymentService.Money(b, b.Payments);
        var link = $"{app.Value.PublicBaseUrl.TrimEnd('/')}/bookings/{b.Code}";
        var lookup = $"{app.Value.PublicBaseUrl.TrimEnd('/')}/lookup";
        var html = new StringBuilder();

        string subject;
        switch (kind)
        {
            case EmailKind.Created:
                subject = $"[{b.Code}] Đã giữ chỗ – vui lòng chuyển khoản cọc";
                html.Append(P($"Cảm ơn {E(b.GuestName)} đã đặt phòng tại <b>{E(h.Name)}</b>. Phòng được giữ tới "
                              + $"<b>{E(Time(b.HoldUntil))}</b> – chuyển khoản cọc <b>{Vnd.Format(b.DepositAmount)}</b> trước thời điểm này để xác nhận."));
                if (bank.Value.IsConfigured)
                    html.Append(Table("Hướng dẫn chuyển khoản",
                        ("Ngân hàng", bank.Value.BankName!), ("Số tài khoản", bank.Value.AccountNumber!),
                        ("Chủ tài khoản", bank.Value.AccountName ?? ""), ("Số tiền", Vnd.Format(Math.Max(0, b.DepositAmount - money.Paid))),
                        ("Nội dung", b.Code)));
                else html.Append(P("Chủ nhà sẽ liên hệ qua số điện thoại của bạn để hướng dẫn đặt cọc."));
                break;
            case EmailKind.Confirmed:
                subject = $"[{b.Code}] Đặt phòng đã được xác nhận";
                html.Append(P($"Đặt phòng của {E(b.GuestName)} tại <b>{E(h.Name)}</b> đã được xác nhận. Hẹn gặp bạn ngày "
                              + $"<b>{Day(b.CheckIn)}</b> từ {CatalogService.Time(h.CheckInTime)}."));
                break;
            case EmailKind.Cancelled:
                subject = $"[{b.Code}] Đặt phòng đã huỷ";
                html.Append(P($"Đặt phòng của {E(b.GuestName)} tại <b>{E(h.Name)}</b> đã được huỷ"
                              + (b.CancelReason is null ? "." : $" – lý do: {E(b.CancelReason)}.")));
                html.Append(P(b.RefundAmount > 0
                    ? $"Số tiền được hoàn: <b>{Vnd.Format(b.RefundAmount)}</b>. Chủ nhà sẽ chuyển trả trong vài ngày làm việc."
                    : "Theo chính sách huỷ, đặt phòng này không được hoàn tiền."));
                break;
            default:
                subject = $"[{b.Code}] Nhắc lịch nhận phòng ngày mai";
                html.Append(P($"Ngày mai <b>{Day(b.CheckIn)}</b> bạn nhận phòng tại <b>{E(h.Name)}</b>, {E(h.Address)}, {E(h.Province)} "
                              + $"từ {CatalogService.Time(h.CheckInTime)}."
                              + (money.AmountDue > 0 ? $" Số tiền còn lại thanh toán khi nhận phòng: <b>{Vnd.Format(money.AmountDue)}</b>." : "")));
                break;
        }

        html.Append(Table("Thông tin đặt phòng",
            ("Mã đặt phòng", b.Code),
            ("Homestay", $"{h.Name} – {h.Address}, {h.Province}"),
            ("Phòng", r.Name),
            ("Nhận phòng", $"{Day(b.CheckIn)} từ {CatalogService.Time(h.CheckInTime)}"),
            ("Trả phòng", $"{Day(b.CheckOut)} trước {CatalogService.Time(h.CheckOutTime)}"),
            ("Số đêm · khách", $"{b.Nights} đêm · {b.Guests} khách"),
            ("Tổng tiền", Vnd.Format(b.Total)),
            ("Tiền cọc", Vnd.Format(b.DepositAmount)),
            ("Đã thanh toán", Vnd.Format(money.Paid))));
        html.Append(P($"Xem chi tiết: <a href=\"{E(link)}\">{E(link)}</a> (đăng nhập tài khoản đã đặt) hoặc "
                      + $"<a href=\"{E(lookup)}\">tra cứu</a> bằng mã đặt phòng + số điện thoại."));

        var body = "<!doctype html><html lang=\"vi\"><body style=\"font-family:Segoe UI,Arial,sans-serif;color:#0f172a;line-height:1.5;max-width:600px\">"
                   + $"<h2 style=\"color:#0f766e\">{E(subject)}</h2>{html}"
                   + "<p style=\"color:#64748b;font-size:12px\">Thư gửi tự động – vui lòng không trả lời thư này.</p></body></html>";
        return (subject, body);
    }

    private string Day(DateOnly d) => d.ToString("dddd, dd/MM/yyyy", Vi);

    private string Time(DateTime? utc) => utc is { } t ? clock.LocalTime(t).ToString("HH:mm dd/MM/yyyy", Vi) : "";

    private static string E(string text) => Html.Encode(text);

    private static string P(string html) => $"<p>{html}</p>";

    private static string Table(string title, params (string Label, string Value)[] rows)
    {
        var sb = new StringBuilder($"<h3 style=\"margin:20px 0 8px\">{E(title)}</h3><table style=\"border-collapse:collapse;width:100%\">");
        foreach (var (label, value) in rows)
            sb.Append($"<tr><td style=\"padding:4px 12px 4px 0;color:#475569;white-space:nowrap\">{E(label)}</td><td style=\"padding:4px 0\"><b>{E(value)}</b></td></tr>");
        return sb.Append("</table>").ToString();
    }
}
