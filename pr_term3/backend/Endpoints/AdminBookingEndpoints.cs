using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>/api/admin – vận hành của lễ tân: đặt phòng, khoản thu, khoá phòng, lịch phòng, bảng điều khiển (policy Staff).</summary>
public static class AdminBookingEndpoints
{
    public static RouteGroupBuilder MapAdminBookings(this RouteGroupBuilder admin)
    {
        var bookings = admin.MapGroup("/bookings").WithTags("Quản trị – đặt phòng");
        bookings.MapGet("", List).WithName("AdminListBookings")
            .WithSummary("Danh sách (status=pending,confirmed, homestayId, from, to, q: mã / tên / SĐT, source, page)");
        bookings.MapPost("", Create).WithName("AdminCreateBooking").WithSummary("Tạo hộ khách (kèm cọc thu ngay → xác nhận)");
        bookings.MapGet("/{id:long}", Get).WithName("AdminGetBooking").WithSummary("Chi tiết + khoản thu + nhật ký");
        bookings.MapPut("/{id:long}", Update).WithName("AdminUpdateBooking").WithSummary("Sửa thông tin khách, ghi chú nội bộ");
        bookings.MapPost("/{id:long}/reschedule", Reschedule).WithName("RescheduleBooking").WithSummary("Đổi ngày / phòng");
        bookings.MapPost("/{id:long}/confirm", Confirm).WithName("ConfirmBooking").WithSummary("Xác nhận (chưa đủ cọc → bắt buộc lý do)");
        bookings.MapPost("/{id:long}/check-in", CheckIn).WithName("CheckInBooking").WithSummary("Nhận phòng");
        bookings.MapPost("/{id:long}/check-out", CheckOut).WithName("CheckOutBooking")
            .WithSummary("Trả phòng (còn phải thu → bắt buộc lý do; trả sớm nhả đêm còn lại)");
        bookings.MapPost("/{id:long}/no-show", NoShow).WithName("NoShowBooking").WithSummary("Khách không đến (sau ngày nhận phòng)");
        bookings.MapGet("/{id:long}/cancel-preview", CancelPreview).WithName("AdminCancelPreview").WithSummary("Số hoàn theo chính sách");
        bookings.MapPost("/{id:long}/cancel", Cancel).WithName("AdminCancelBooking").WithSummary("Huỷ (bắt buộc lý do, sửa được số hoàn)");
        bookings.MapPost("/{id:long}/payments", RecordPayment).WithName("RecordPayment").WithSummary("Ghi khoản thu / hoàn");
        bookings.MapDelete("/{id:long}/payments/{paymentId:long}", DeletePayment).WithName("DeletePayment")
            .WithSummary("Xoá khoản ghi nhầm (lễ tân: trong 24 giờ; admin: bất kỳ lúc nào)");

        var ops = admin.MapGroup("").WithTags("Quản trị – lịch phòng");
        ops.MapGet("/calendar", Calendar).WithName("AdminCalendar")
            .WithSummary($"Lưới phòng × ngày: đặt phòng, khoá, giá (homestayId, from, to – tối đa {CalendarService.MaxCalendarDays} ngày)");
        ops.MapGet("/dashboard", Dashboard).WithName("AdminDashboard").WithSummary("Hôm nay: đến, đi, đang ở, chờ cọc, công suất 7 ngày");
        ops.MapGet("/blocks", ListBlocks).WithName("ListBlocks").WithSummary("Khoá phòng còn hiệu lực từ ngày from (mặc định hôm nay)");
        ops.MapPost("/blocks", CreateBlock).WithName("CreateBlock").WithSummary("Khoá phòng bảo trì [from, to] – trùng đặt phòng → 409");
        ops.MapDelete("/blocks/{id:long}", DeleteBlock).WithName("DeleteBlock").WithSummary("Bỏ khoá phòng");

        return admin;
    }

    private static readonly PaymentKind[] UpfrontKinds = [PaymentKind.Deposit, PaymentKind.Balance];

    private static async Task<Results<Ok<PagedResult<BookingSummaryDto>>, ValidationProblem>> List(
        string? status, long? homestayId, DateOnly? from, DateOnly? to, string? q, string? source, int? page,
        BookingQueryService queries, CancellationToken ct)
    {
        var v = new RequestValidator()
            .EnumList("status", status, out BookingStatus[] statuses, "Trạng thái")
            .OptionalText("q", q, 100, out var term, "Từ khoá")
            .Int("page", page, 1, 10_000, 1, out var pageNumber, "Trang");
        BookingSource? sourceFilter = null;
        if (!string.IsNullOrWhiteSpace(source))
        {
            v.Enum("source", source, out BookingSource parsed, "Nguồn");
            sourceFilter = parsed;
        }
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await queries.AdminListAsync(new BookingFilter(statuses, homestayId, from, to, term, sourceFilter, pageNumber), ct));
    }

    private static async Task<Results<Created<BookingDto>, ValidationProblem>> Create(
        AdminCreateBookingRequest body, ClaimsPrincipal user, AppClock clock, BookingService bookings, BookingQueryService queries,
        CancellationToken ct)
    {
        var v = BookingEndpoints.ValidateStay(body.RoomId, body.CheckIn, body.CheckOut, body.Guests, clock, out var stay);
        ValidateGuest(v, body.GuestName, body.GuestPhone, body.GuestEmail, body.Note, out var guest)
            .OptionalText("internalNote", body.InternalNote, 500, out var internalNote, "Ghi chú nội bộ")
            .Enum("source", body.Source ?? "phone", out BookingSource source, "Nguồn");
        PaymentInput? payment = null;
        if (body.Payment is { } p)
        {
            ValidatePayment(v, p, "payment.", UpfrontKinds, out var input);
            payment = input;
        }
        if (!v.IsValid) return v.Problem();

        var id = await bookings.CreateAsync(user.GetActor(), onBehalf: true, stay, guest, source, internalNote, expectedTotal: null, payment, ct);
        return TypedResults.Created($"/api/admin/bookings/{id}", await queries.AdminDetailAsync(id, ct));
    }

    private static async Task<Ok<BookingDto>> Get(long id, BookingQueryService queries, CancellationToken ct) =>
        TypedResults.Ok(await queries.AdminDetailAsync(id, ct));

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> Update(
        long id, UpdateBookingRequest body, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct)
    {
        var v = ValidateGuest(new RequestValidator(), body.GuestName, body.GuestPhone, body.GuestEmail, body.GuestNote, out var guest, "guestNote")
            .OptionalText("internalNote", body.InternalNote, 500, out var internalNote, "Ghi chú nội bộ");
        if (!v.IsValid) return v.Problem();
        await bookings.UpdateAsync(id, user.GetActor(), guest, internalNote, ct);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> Reschedule(
        long id, RescheduleRequest body, ClaimsPrincipal user, AppClock clock, BookingService bookings, BookingQueryService queries,
        CancellationToken ct)
    {
        var current = await queries.AdminDetailAsync(id, ct);
        var v = BookingEndpoints.ValidateStay(body.RoomId ?? current.RoomId, body.CheckIn, body.CheckOut, body.Guests, clock, out var stay,
                fallbackGuests: current.Guests)
            .OptionalText("reason", body.Reason, 300, out var reason, "Lý do");
        var keepPrice = body.KeepPrice ?? false;
        if (keepPrice && reason is null) v.Add("reason", "Giữ giá cũ cần ghi lý do");
        if (!v.IsValid) return v.Problem();

        await bookings.RescheduleAsync(id, user.GetActor(), stay, keepPrice, reason, ct);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static Task<Results<Ok<BookingDto>, ValidationProblem>> Confirm(
        long id, ReasonRequest? body, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct) =>
        WithReason(id, body, queries, ct, reason => bookings.ConfirmAsync(id, user.GetActor(), reason, ct));

    private static async Task<Ok<BookingDto>> CheckIn(
        long id, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct)
    {
        await bookings.CheckInAsync(id, user.GetActor(), ct);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static Task<Results<Ok<BookingDto>, ValidationProblem>> CheckOut(
        long id, ReasonRequest? body, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct) =>
        WithReason(id, body, queries, ct, reason => bookings.CheckOutAsync(id, user.GetActor(), reason, ct));

    private static Task<Results<Ok<BookingDto>, ValidationProblem>> NoShow(
        long id, ReasonRequest? body, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct) =>
        WithReason(id, body, queries, ct, reason => bookings.NoShowAsync(id, user.GetActor(), reason, ct));

    private static async Task<Ok<CancelPreviewDto>> CancelPreview(long id, BookingService bookings, CancellationToken ct) =>
        TypedResults.Ok(await bookings.CancelPreviewAsync(id, byStaff: true, ct));

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> Cancel(
        long id, CancelRequest body, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct)
    {
        var v = new RequestValidator().Text("reason", body.Reason, 500, out var reason, "Lý do huỷ");
        if (body.RefundAmount is not null) v.Money("refundAmount", body.RefundAmount, out _, "Số hoàn", allowZero: true);
        if (!v.IsValid) return v.Problem();

        await bookings.CancelAsync(id, user.GetActor(), byStaff: true, reason, body.RefundAmount, ct);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> RecordPayment(
        long id, PaymentRequest body, ClaimsPrincipal user, PaymentService payments, BookingQueryService queries, CancellationToken ct)
    {
        var v = ValidatePayment(new RequestValidator(), body, "", Enum.GetValues<PaymentKind>(), out var input);
        if (!v.IsValid) return v.Problem();
        await payments.RecordAsync(id, user.GetActor(), input, ct);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static async Task<Ok<BookingDto>> DeletePayment(
        long id, long paymentId, ClaimsPrincipal user, PaymentService payments, BookingQueryService queries, CancellationToken ct)
    {
        await payments.DeleteAsync(id, paymentId, user.GetActor(), ct);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static async Task<Results<Ok<AdminCalendarDto>, ValidationProblem>> Calendar(
        long? homestayId, DateOnly? from, DateOnly? to, AppClock clock, CalendarService calendar, CancellationToken ct)
    {
        var start = from ?? clock.Today;
        var v = new RequestValidator().DateRange(start, to ?? start.AddDays(13), CalendarService.MaxCalendarDays, out var f, out var t);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await calendar.CalendarAsync(homestayId, f, t, ct));
    }

    private static async Task<Ok<DashboardDto>> Dashboard(DateOnly? date, AppClock clock, CalendarService calendar, CancellationToken ct) =>
        TypedResults.Ok(await calendar.DashboardAsync(date ?? clock.Today, ct));

    private static async Task<Ok<List<BlockDto>>> ListBlocks(
        long? homestayId, DateOnly? from, AppClock clock, CalendarService calendar, CancellationToken ct) =>
        TypedResults.Ok(await calendar.ListBlocksAsync(homestayId, from ?? clock.Today, ct));

    private static async Task<Results<Created<BlockDto>, ValidationProblem>> CreateBlock(
        BlockRequest body, ClaimsPrincipal user, AppClock clock, CalendarService calendar, CancellationToken ct)
    {
        var v = new RequestValidator()
            .DateRange(body.From, body.To, CalendarService.MaxBlockDays, out var from, out var to)
            .Text("reason", body.Reason, 200, out var reason, "Lý do khoá");
        if (body.RoomId is null or <= 0) v.Add("roomId", "Vui lòng chọn phòng");
        if (body.From is not null && from < clock.Today) v.Add("from", "Ngày bắt đầu không được ở quá khứ");
        if (!v.IsValid) return v.Problem();

        var block = await calendar.CreateBlockAsync(user.GetActor(), body.RoomId!.Value, from, to, reason, ct);
        return TypedResults.Created($"/api/admin/blocks/{block.Id}", block);
    }

    private static async Task<NoContent> DeleteBlock(long id, CalendarService calendar, CancellationToken ct)
    {
        await calendar.DeleteBlockAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> WithReason(
        long id, ReasonRequest? body, BookingQueryService queries, CancellationToken ct, Func<string?, Task> action)
    {
        var v = new RequestValidator().OptionalText("reason", body?.Reason, 300, out var reason, "Lý do");
        if (!v.IsValid) return v.Problem();
        await action(reason);
        return TypedResults.Ok(await queries.AdminDetailAsync(id, ct));
    }

    private static RequestValidator ValidateGuest(
        RequestValidator v, string? name, string? phone, string? email, string? note, out GuestInput guest, string noteField = "note")
    {
        v.Text("guestName", name, 100, out var guestName, "Họ tên khách")
            .Phone("guestPhone", phone, out var guestPhone, required: true)
            .OptionalText(noteField, note, 500, out var guestNote, "Ghi chú của khách");
        string? guestEmail = null;
        if (!string.IsNullOrWhiteSpace(email)) v.Email(email, out guestEmail, "guestEmail");
        guest = new GuestInput(guestName, guestPhone ?? "", guestEmail, guestNote);
        return v;
    }

    private static RequestValidator ValidatePayment(RequestValidator v, PaymentRequest p, string prefix, PaymentKind[] kinds, out PaymentInput input)
    {
        v.Enum($"{prefix}kind", p.Kind ?? (prefix.Length > 0 ? "deposit" : null), out PaymentKind kind, "Loại khoản", kinds)
            .Enum($"{prefix}method", p.Method, out PaymentMethod method, "Hình thức thanh toán")
            .Money($"{prefix}amount", p.Amount, out var amount, "Số tiền")
            .OptionalText($"{prefix}note", p.Note, 200, out var note, "Ghi chú");
        input = new PaymentInput(kind, method, amount, PaymentService.Utc(p.PaidAt), note);
        return v;
    }
}
