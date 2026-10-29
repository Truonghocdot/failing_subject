using backend.Auth;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;

namespace backend.Services;

/// <summary>Tài khoản nhận cọc hiện trong hướng dẫn chuyển khoản (Payment__BankName... trong .env) – trống thì không hiện.</summary>
public sealed class PaymentOptions
{
    public const string Section = "Payment";

    public string? BankName { get; init; }
    public string? AccountNumber { get; init; }
    public string? AccountName { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BankName) && !string.IsNullOrWhiteSpace(AccountNumber);
}

public sealed record PaymentInput(PaymentKind Kind, PaymentMethod Method, decimal Amount, DateTime? PaidAt, string? Note);

/// <summary>Ghi / xoá khoản thu – hoàn (docs/02-nghiep-vu.md §8). Đủ cọc → đặt phòng chờ cọc tự chuyển "đã xác nhận".</summary>
public sealed class PaymentService(AppDbContext db, AppClock clock)
{
    /// <summary>Nhân viên chỉ xoá được khoản ghi nhầm trong 24 giờ; admin xoá lúc nào cũng được.</summary>
    public static readonly TimeSpan StaffDeleteWindow = TimeSpan.FromHours(24);

    public async Task RecordAsync(long bookingId, Actor actor, PaymentInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(bookingId, ct);
        var money = Money(b, b.Payments);

        if (input.Kind == PaymentKind.Refund)
        {
            if (input.Amount > money.Paid)
                throw Invalid("amount", $"Số hoàn không được vượt số đã thu ({Vnd.Format(money.Paid)})");
        }
        else if (b.Status is BookingStatus.Cancelled or BookingStatus.Expired or BookingStatus.NoShow)
        {
            throw ApiProblemException.Conflict($"Đặt phòng {BookingLocks.Label(b.Status)} – chỉ ghi được khoản hoàn");
        }

        var now = clock.UtcNow;
        var paidAt = Utc(input.PaidAt) ?? now;
        if (paidAt > now.AddMinutes(5)) throw Invalid("paidAt", "Thời điểm thu không được ở tương lai");

        b.Payments.Add(new Payment
        {
            BookingId = b.Id, Kind = input.Kind, Method = input.Method, Amount = input.Amount, PaidAt = paidAt,
            Note = input.Note, RecordedById = actor.Id,
        });
        db.Log(b, "payment", $"{KindLabel(input.Kind)} {Vnd.Format(input.Amount)} ({MethodLabel(input.Method)})"
                             + (input.Note is null ? "" : $" – {input.Note}"), actor.Id);
        AutoConfirm(b, actor.Id, now);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DeleteAsync(long bookingId, long paymentId, Actor actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(bookingId, ct);
        var payment = b.Payments.SingleOrDefault(p => p.Id == paymentId) ?? throw ApiProblemException.NotFound("Không tìm thấy khoản thu");

        if (!actor.IsAdmin && clock.UtcNow - payment.CreatedAt > StaffDeleteWindow)
            throw ApiProblemException.Conflict("Chỉ xoá được khoản thu trong 24 giờ sau khi ghi – liên hệ quản trị viên");
        if (payment.Kind != PaymentKind.Refund && Money(b, b.Payments.Where(p => p != payment)).Paid < 0)
            throw ApiProblemException.Conflict("Xoá khoản này làm số đã hoàn lớn hơn số đã thu – hãy xoá khoản hoàn trước");

        db.Payments.Remove(payment);
        db.Log(b, "payment_deleted",
            $"Xoá khoản ghi nhầm: {KindLabel(payment.Kind)} {Vnd.Format(payment.Amount)} ({MethodLabel(payment.Method)})", actor.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Đặt phòng chờ cọc đã nhận đủ cọc → xác nhận (dùng cả khi tạo hộ kèm khoản cọc).</summary>
    public void AutoConfirm(Booking b, Guid actorId, DateTime now)
    {
        if (b.Status != BookingStatus.Pending || Money(b, b.Payments).Paid < b.DepositAmount) return;
        b.ConfirmedAt = now;
        b.HoldUntil = null;
        db.Transition(b, BookingStatus.Confirmed, actorId, "Tự xác nhận – đã nhận đủ cọc");
    }

    /// <summary>
    /// paid = Σ (cọc + phần còn lại + phụ thu) − Σ hoàn; amountDue = total + Σ phụ thu − paid (đặt phòng đã đóng: 0);
    /// pendingRefund = số hoàn đã chốt khi huỷ − số đã hoàn.
    /// </summary>
    public static MoneyDto Money(Booking b, IEnumerable<Payment> payments)
    {
        decimal collected = 0, extras = 0, refunded = 0;
        foreach (var p in payments)
        {
            if (p.Kind == PaymentKind.Refund) refunded += p.Amount;
            else collected += p.Amount;
            if (p.Kind == PaymentKind.Extra) extras += p.Amount;
        }

        var paid = collected - refunded;
        var closed = b.Status is BookingStatus.Cancelled or BookingStatus.Expired or BookingStatus.NoShow;
        var amountDue = closed ? 0 : Math.Max(0, b.Total + extras - paid);
        var pendingRefund = b.Status is BookingStatus.Cancelled or BookingStatus.Expired ? Math.Max(0, b.RefundAmount - refunded) : 0;
        var status = closed && refunded > 0 && pendingRefund == 0 ? PaymentStatus.Refunded
            : paid <= 0 ? PaymentStatus.Unpaid
            : paid >= b.Total + extras ? PaymentStatus.Paid
            : PaymentStatus.DepositPaid;
        return new MoneyDto(paid, extras, refunded, amountDue, pendingRefund, status);
    }

    public static string KindLabel(PaymentKind kind) => kind switch
    {
        PaymentKind.Deposit => "Thu cọc",
        PaymentKind.Balance => "Thu phần còn lại",
        PaymentKind.Extra => "Thu phụ thu",
        _ => "Hoàn tiền",
    };

    public static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "tiền mặt",
        PaymentMethod.BankTransfer => "chuyển khoản",
        PaymentMethod.Card => "thẻ",
        _ => "ví điện tử",
    };

    /// <summary>Thời điểm từ JSON: có múi giờ ("…Z", "+07:00") → đổi về UTC; không có → coi như UTC.</summary>
    public static DateTime? Utc(DateTime? value) => value is not { } v ? null
        : DateTime.SpecifyKind(v.Kind == DateTimeKind.Unspecified ? v : v.ToUniversalTime(), DateTimeKind.Utc);

    private static ApiProblemException Invalid(string field, string message) =>
        ApiProblemException.Validation(new Dictionary<string, string[]> { [field] = [message] });
}
