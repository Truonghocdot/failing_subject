using System.Security.Cryptography;
using backend.Auth;
using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed record StayInput(long RoomId, DateOnly CheckIn, DateOnly CheckOut, int Guests);

public sealed record GuestInput(string Name, string Phone, string? Email, string? Note);

/// <summary>
/// Tạo đặt phòng + vòng đời (docs/02-nghiep-vu.md §6–7). Mọi thay đổi chạy trong transaction:
/// khoá dòng booking trước, khoá / nhả room_nights cùng transaction, ghi booking_events.
/// </summary>
public sealed class BookingService(AppDbContext db, AppClock clock, PricingService pricing, PaymentService payments, EmailService emails)
{
    public const string NotFound = "Không tìm thấy đặt phòng";

    // Crockford base32: không có I, L, O, U – đọc qua điện thoại không nhầm
    private const string CodeAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// onBehalf = nhân viên tạo hộ (không gắn tài khoản khách, ghi người tạo); payment = cọc thu ngay.
    /// Khách đặt online: giữ chỗ holdHours giờ chờ cọc; homestay không cần cọc → xác nhận ngay.
    /// </summary>
    public async Task<long> CreateAsync(
        Actor actor, bool onBehalf, StayInput stay, GuestInput guest, BookingSource source, string? internalNote,
        decimal? expectedTotal, PaymentInput? payment, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().Include(r => r.Homestay).SingleOrDefaultAsync(r => r.Id == stay.RoomId, ct);
        var bookable = room is { Status: RoomStatus.Active, Homestay.Status: HomestayStatus.Active };
        if (room is null || (!bookable && !onBehalf)) throw ApiProblemException.NotFound("Không tìm thấy phòng");
        if (!bookable) throw ApiProblemException.Conflict("Phòng / homestay đang ngưng nhận đặt");

        var nights = stay.CheckOut.DayNumber - stay.CheckIn.DayNumber;
        StayRules.EnsureRoomFits(room, nights, stay.Guests);
        var price = Pricing.Calculate(room, room.Homestay.DepositPercent, stay.CheckIn, stay.CheckOut, stay.Guests,
            await pricing.OverridesAsync(room.Id, stay.CheckIn, stay.CheckOut, ct));
        if (expectedTotal is { } expected && expected != price.Total)
            throw ApiProblemException.Conflict($"Giá đã thay đổi: tổng mới {Vnd.Format(price.Total)} – vui lòng xem lại và xác nhận")
                .With("quote", AvailabilityService.ToQuote(room, stay.CheckIn, stay.CheckOut, stay.Guests, price, available: true));
        if (payment is not null && payment.Amount > price.Total)
            throw ApiProblemException.Validation(new Dictionary<string, string[]>
            {
                ["payment.amount"] = [$"Số thu không được vượt tổng tiền ({Vnd.Format(price.Total)})"],
            });
        // Kiểm tra sớm cho thông báo nhanh; chốt chặn thật là khoá chính room_nights bên dưới
        if (await db.RoomNights.AnyAsync(n => n.RoomId == room.Id && n.Night >= stay.CheckIn && n.Night < stay.CheckOut, ct))
            throw ApiProblemException.Conflict(BookingLocks.NightsTaken);

        var now = clock.UtcNow;
        var noDeposit = price.DepositAmount == 0;
        var booking = new Booking
        {
            Code = NewCode(),
            HomestayId = room.HomestayId,
            RoomId = room.Id,
            UserId = onBehalf ? null : actor.Id,
            GuestName = guest.Name,
            GuestPhone = guest.Phone,
            GuestEmail = guest.Email,
            Guests = (byte)stay.Guests,
            Nights = (short)nights,
            CheckIn = stay.CheckIn,
            CheckOut = stay.CheckOut,
            RoomTotal = price.RoomTotal,
            ExtraGuestTotal = price.ExtraGuestTotal,
            CleaningFee = price.CleaningFee,
            Total = price.Total,
            DepositAmount = price.DepositAmount,
            Status = noDeposit ? BookingStatus.Confirmed : BookingStatus.Pending,
            Source = source,
            GuestNote = guest.Note,
            InternalNote = internalNote,
            HoldUntil = noDeposit ? null : now.AddHours(room.Homestay.HoldHours),
            ConfirmedAt = noDeposit ? now : null,
            CreatedById = onBehalf ? actor.Id : null,
            NightRates = [.. price.Nights.Select(n => new BookingNight { Night = n.Date, Price = n.Price })],
        };

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Bookings.Add(booking);
        await SaveWithUniqueCodeAsync(booking, ct);
        db.BookingEvents.Add(new BookingEvent
        {
            BookingId = booking.Id, Type = "created", ToStatus = booking.Status, ActorId = actor.Id,
            Message = $"Tạo đặt phòng ({SourceLabel(source)}) – tổng {Vnd.Format(booking.Total)}, cọc {Vnd.Format(booking.DepositAmount)}",
        });
        await db.LockNightsAsync(room.Id, stay.CheckIn, stay.CheckOut, booking.Id, null, ct);

        if (payment is not null)
        {
            booking.Payments.Add(new Payment
            {
                BookingId = booking.Id, Kind = payment.Kind, Method = payment.Method, Amount = payment.Amount,
                PaidAt = payment.PaidAt ?? now, Note = payment.Note, RecordedById = actor.Id,
            });
            db.Log(booking, "payment", $"{PaymentService.KindLabel(payment.Kind)} {Vnd.Format(payment.Amount)} " +
                                       $"({PaymentService.MethodLabel(payment.Method)}) khi tạo đặt phòng", actor.Id);
            payments.AutoConfirm(booking, actor.Id, now);
        }
        // Chờ cọc → thư kèm hướng dẫn chuyển khoản; đã đủ cọc / không cần cọc → thư xác nhận
        emails.Queue(booking, room.Homestay, room, booking.Status == BookingStatus.Confirmed ? EmailKind.Confirmed : EmailKind.Created);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return booking.Id;
    }

    /// <summary>Đặt phòng của chính khách (theo mã); của người khác → 404 như không tồn tại.</summary>
    public async Task<long> FindOwnAsync(Guid userId, string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var id = await db.Bookings.Where(b => b.Code == normalized && b.UserId == userId).Select(b => b.Id).SingleOrDefaultAsync(ct);
        return id != 0 ? id : throw ApiProblemException.NotFound(NotFound);
    }

    public async Task<CancelPreviewDto> CancelPreviewAsync(long id, bool byStaff, CancellationToken ct)
    {
        var b = await db.Bookings.AsNoTracking().Include(x => x.Homestay).Include(x => x.Payments)
                    .SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw ApiProblemException.NotFound(NotFound);
        return Preview(b, byStaff);
    }

    /// <summary>
    /// Khách: trước ngày nhận phòng; nhân viên: tới hết ngày nhận phòng. Số hoàn theo chính sách homestay trên số đã trả
    /// (docs/02-nghiep-vu.md §7); nhân viên sửa được số hoàn (ghi lý do).
    /// </summary>
    public async Task CancelAsync(long id, Actor actor, bool byStaff, string? reason, decimal? refundOverride, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        var preview = Preview(b, byStaff);
        if (!preview.CanCancel) throw ApiProblemException.Conflict(preview.Reason!);

        var refund = preview.RefundAmount;
        var message = $"Huỷ{(reason is null ? "" : $": {reason}")}. Hoàn {Vnd.Format(refund)}";
        if (byStaff && refundOverride is { } custom && custom != refund)
        {
            if (custom > preview.Paid)
                throw ApiProblemException.Validation(new Dictionary<string, string[]>
                {
                    ["refundAmount"] = [$"Số hoàn không được vượt số khách đã trả ({Vnd.Format(preview.Paid)})"],
                });
            message = $"Huỷ: {reason}. Hoàn {Vnd.Format(custom)} (nhân viên sửa – theo chính sách {Vnd.Format(refund)})";
            refund = custom;
        }

        b.RefundAmount = refund;
        b.CancelReason = reason;
        b.CancelledAt = clock.UtcNow;
        b.HoldUntil = null;
        db.Transition(b, BookingStatus.Cancelled, actor.Id, BookingLocks.Truncate(message));
        emails.Queue(b, b.Homestay, b.Room, EmailKind.Cancelled);
        await db.ReleaseNightsAsync(b.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Xác nhận tay khi chưa đủ cọc (VD khách quen) – bắt buộc lý do.</summary>
    public async Task ConfirmAsync(long id, Actor actor, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        if (b.Status != BookingStatus.Pending) throw ApiProblemException.Conflict("Chỉ xác nhận được đặt phòng đang chờ cọc");

        var paid = PaymentService.Money(b, b.Payments).Paid;
        if (paid < b.DepositAmount && reason is null)
            throw ApiProblemException.Validation(new Dictionary<string, string[]>
            {
                ["reason"] = [$"Chưa nhận đủ cọc ({Vnd.Format(paid)} / {Vnd.Format(b.DepositAmount)}) – nhập lý do để xác nhận"],
            });

        b.ConfirmedAt = clock.UtcNow;
        b.HoldUntil = null;
        db.Transition(b, BookingStatus.Confirmed, actor.Id, reason is null ? "Xác nhận" : $"Xác nhận: {reason}");
        emails.Queue(b, b.Homestay, b.Room, EmailKind.Confirmed);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task CheckInAsync(long id, Actor actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        if (b.Status != BookingStatus.Confirmed) throw ApiProblemException.Conflict("Chỉ check-in được đặt phòng đã xác nhận");
        var today = clock.Today;
        if (today < b.CheckIn) throw ApiProblemException.Conflict($"Chưa tới ngày nhận phòng ({b.CheckIn:dd/MM/yyyy})");
        if (today >= b.CheckOut) throw ApiProblemException.Conflict("Đã qua ngày trả phòng – không check-in được");

        b.CheckedInAt = clock.UtcNow;
        db.Transition(b, BookingStatus.CheckedIn, actor.Id, "Nhận phòng");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Còn phải thu → phải nhập lý do bỏ qua. Trả sớm → nhả các đêm còn lại (phòng trống lại ngay).</summary>
    public async Task CheckOutAsync(long id, Actor actor, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        if (b.Status != BookingStatus.CheckedIn) throw ApiProblemException.Conflict("Chỉ check-out được khách đang ở");

        var due = PaymentService.Money(b, b.Payments).AmountDue;
        if (due > 0 && reason is null)
            throw ApiProblemException.Conflict($"Khách còn phải thu {Vnd.Format(due)} – thu đủ hoặc nhập lý do bỏ qua", "reason");

        var today = clock.Today;
        var message = "Trả phòng";
        if (today < b.CheckOut) message += $" sớm – nhả {b.CheckOut.DayNumber - Max(today, b.CheckIn).DayNumber} đêm còn lại"; // mọi đêm từ hôm nay đều trống lại (kể cả trả ngay trong ngày nhận phòng)
        if (due > 0) message += $"; còn thiếu {Vnd.Format(due)}: {reason}";

        b.CheckedOutAt = clock.UtcNow;
        db.Transition(b, BookingStatus.CheckedOut, actor.Id, BookingLocks.Truncate(message));
        await db.ReleaseNightsAsync(b.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Khách không đến (sau ngày nhận phòng): giữ tiền cọc, nhả đêm.</summary>
    public async Task NoShowAsync(long id, Actor actor, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        if (b.Status != BookingStatus.Confirmed) throw ApiProblemException.Conflict("Chỉ đánh dấu không đến cho đặt phòng đã xác nhận");
        if (clock.Today <= b.CheckIn) throw ApiProblemException.Conflict("Chỉ đánh dấu không đến từ ngày sau ngày nhận phòng");

        var paid = PaymentService.Money(b, b.Payments).Paid;
        db.Transition(b, BookingStatus.NoShow, actor.Id,
            BookingLocks.Truncate($"Khách không đến – giữ {Vnd.Format(paid)} đã thu{(reason is null ? "" : $": {reason}")}"));
        await db.ReleaseNightsAsync(b.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Đổi ngày / phòng khi chờ cọc hoặc đã xác nhận: nhả đêm cũ + khoá đêm mới trong 1 transaction (trùng → 409, giữ nguyên đặt cũ).
    /// keepPrice: giữ giá đã chốt (cùng số đêm); không thì tính lại theo giá hiện tại của phòng mới.
    /// </summary>
    public async Task RescheduleAsync(long id, Actor actor, StayInput stay, bool keepPrice, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        if (b.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            throw ApiProblemException.Conflict("Chỉ đổi ngày / phòng khi đặt phòng đang chờ cọc hoặc đã xác nhận");

        var room = await db.Rooms.Include(r => r.Homestay).SingleOrDefaultAsync(r => r.Id == stay.RoomId, ct)
                   ?? throw ApiProblemException.NotFound("Không tìm thấy phòng");
        if (room.Status != RoomStatus.Active || room.Homestay.Status != HomestayStatus.Active)
            throw ApiProblemException.Conflict("Phòng / homestay đang ngưng nhận đặt");
        var nights = stay.CheckOut.DayNumber - stay.CheckIn.DayNumber;
        StayRules.EnsureRoomFits(room, nights, stay.Guests);

        var before = $"{b.Room.Name} {b.CheckIn:dd/MM}→{b.CheckOut:dd/MM}, {b.Guests} khách, {Vnd.Format(b.Total)}";
        List<BookingNight> newNights;
        if (keepPrice)
        {
            if (nights != b.Nights)
                throw ApiProblemException.Validation(new Dictionary<string, string[]>
                {
                    ["checkOut"] = [$"Giữ giá cũ chỉ áp dụng khi giữ nguyên số đêm ({b.Nights} đêm)"],
                });
            var oldPrices = await db.BookingNights.Where(n => n.BookingId == b.Id).OrderBy(n => n.Night).Select(n => n.Price).ToListAsync(ct);
            newNights = [.. Pricing.Nights(stay.CheckIn, stay.CheckOut).Zip(oldPrices, (d, p) => new BookingNight { BookingId = b.Id, Night = d, Price = p })];
        }
        else
        {
            var price = Pricing.Calculate(room, room.Homestay.DepositPercent, stay.CheckIn, stay.CheckOut, stay.Guests,
                await pricing.OverridesAsync(room.Id, stay.CheckIn, stay.CheckOut, ct));
            newNights = [.. price.Nights.Select(n => new BookingNight { BookingId = b.Id, Night = n.Date, Price = n.Price })];
            b.RoomTotal = price.RoomTotal;
            b.ExtraGuestTotal = price.ExtraGuestTotal;
            b.CleaningFee = price.CleaningFee;
            b.Total = price.Total;
            b.DepositAmount = price.DepositAmount;
        }

        // Xoá bằng lệnh SQL (không qua change tracker) để thêm lại dòng cùng khoá (booking_id, night) không vướng tracking
        await db.ReleaseNightsAsync(b.Id, ct);
        await db.BookingNights.Where(n => n.BookingId == b.Id).ExecuteDeleteAsync(ct);
        db.BookingNights.AddRange(newNights);

        b.Room = room;
        b.Homestay = room.Homestay;
        b.CheckIn = stay.CheckIn;
        b.CheckOut = stay.CheckOut;
        b.Nights = (short)nights;
        b.Guests = (byte)stay.Guests;
        var after = $"{room.Name} {b.CheckIn:dd/MM}→{b.CheckOut:dd/MM}, {b.Guests} khách, {Vnd.Format(b.Total)}";
        db.Log(b, "rescheduled", $"Đổi {before} ⇒ {after}{(keepPrice ? " (giữ giá cũ)" : "")}{(reason is null ? "" : $": {reason}")}", actor.Id);

        await db.LockNightsAsync(room.Id, stay.CheckIn, stay.CheckOut, b.Id, null, ct);
        await tx.CommitAsync(ct);
    }

    public async Task UpdateAsync(long id, Actor actor, GuestInput guest, string? internalNote, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var b = await db.LockBookingAsync(id, ct);
        b.GuestName = guest.Name;
        b.GuestPhone = guest.Phone;
        b.GuestEmail = guest.Email;
        b.GuestNote = guest.Note;
        b.InternalNote = internalNote;
        if (db.ChangeTracker.HasChanges()) db.Log(b, "updated", "Sửa thông tin khách / ghi chú", actor.Id);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Hết hạn giữ chỗ: pending quá holdUntil → expired, nhả đêm (HoldExpiryWorker gọi mỗi phút). Trả số đặt phòng đã xử lý.</summary>
    public async Task<int> ExpireDueAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending && b.HoldUntil < now)
            .OrderBy(b => b.HoldUntil).Select(b => b.Id).Take(100)
            .ToListAsync(ct);

        var expired = 0;
        foreach (var id in due)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var b = await db.LockBookingAsync(id, ct);
            // Đọc lại sau khi khoá: lễ tân có thể vừa ghi cọc / xác nhận
            if (b.Status == BookingStatus.Pending && b.HoldUntil < now)
            {
                db.Transition(b, BookingStatus.Expired, null, "Hết hạn giữ chỗ – chưa nhận đủ cọc");
                await db.ReleaseNightsAsync(b.Id, ct);
                await db.SaveChangesAsync(ct);
                expired++;
            }
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
        }
        return expired;
    }

    private CancelPreviewDto Preview(Booking b, bool byStaff)
    {
        var daysBefore = b.CheckIn.DayNumber - clock.Today.DayNumber;
        var paid = PaymentService.Money(b, b.Payments).Paid;
        string? blocked = b.Status is not (BookingStatus.Pending or BookingStatus.Confirmed)
            ? "Chỉ huỷ được đặt phòng đang chờ cọc hoặc đã xác nhận"
            : byStaff
                ? daysBefore < 0 ? "Đã qua ngày nhận phòng – hãy đánh dấu khách không đến" : null
                : daysBefore < 1 ? "Đã tới ngày nhận phòng – vui lòng liên hệ homestay để huỷ" : null;

        var h = b.Homestay;
        var percent = paid <= 0 ? 0
            : daysBefore >= h.FreeCancelDays ? 100
            : daysBefore >= h.PartialRefundDays ? h.PartialRefundPercent
            : 0;
        return new CancelPreviewDto(blocked is null, blocked, daysBefore, Math.Max(0, paid), percent, Vnd.Round(Math.Max(0, paid) * percent / 100m));
    }

    private async Task SaveWithUniqueCodeAsync(Booking booking, CancellationToken ct)
    {
        // 32^8 ≈ 10^12 mã → trùng gần như không xảy ra, nhưng unique index vẫn là chốt chặn
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (Db.IsDuplicateKey(ex) && attempt < 3)
            {
                booking.Code = NewCode();
            }
        }
    }

    public static string NewCode() =>
        $"HS-{RandomNumberGenerator.GetString(CodeAlphabet, 4)}-{RandomNumberGenerator.GetString(CodeAlphabet, 4)}";

    public static string SourceLabel(BookingSource source) => source switch
    {
        BookingSource.Web => "đặt online",
        BookingSource.Phone => "qua điện thoại",
        BookingSource.WalkIn => "khách vãng lai",
        _ => "từ OTA",
    };

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
