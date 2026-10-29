using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>/api/bookings – đặt phòng của khách đang đăng nhập (chỉ thấy của mình) + tra cứu công khai bằng mã + SĐT.</summary>
public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookings").WithTags("Khách – đặt phòng");

        group.MapPost("/lookup", Lookup).WithName("LookupBooking").WithSummary("Tra cứu bằng mã đặt phòng + số điện thoại (không cần đăng nhập)")
            .AllowAnonymous().RequireRateLimiting(AuthEndpoints.RateLimitPolicy);

        var mine = group.MapGroup("").RequireAuthorization();
        mine.MapPost("", Create).WithName("CreateBooking")
            .WithSummary("Đặt phòng online – giữ chỗ chờ cọc; giá khác expectedTotal → 409 kèm báo giá mới");
        mine.MapGet("", Mine).WithName("MyBookings").WithSummary("Đặt phòng của tôi (status=pending,confirmed…)");
        mine.MapGet("/{code}", Get).WithName("MyBooking").WithSummary("Chi tiết + hướng dẫn chuyển khoản + các mốc trạng thái");
        mine.MapGet("/{code}/cancel-preview", CancelPreview).WithName("MyBookingCancelPreview").WithSummary("Số tiền được hoàn nếu huỷ bây giờ");
        mine.MapPost("/{code}/cancel", Cancel).WithName("CancelMyBooking").WithSummary("Huỷ đặt phòng (trước ngày nhận phòng)");

        return app;
    }

    private static async Task<Results<Created<BookingDto>, ValidationProblem>> Create(
        CreateBookingRequest body, ClaimsPrincipal user, AppClock clock, AuthService auth,
        BookingService bookings, BookingQueryService queries, CancellationToken ct)
    {
        var v = ValidateStay(body.RoomId, body.CheckIn, body.CheckOut, body.Guests, clock, out var stay)
            .Text("guestName", body.GuestName, 100, out var guestName, "Họ tên khách")
            .Phone("guestPhone", body.GuestPhone, out var guestPhone, required: true)
            .OptionalText("note", body.Note, 500, out var note, "Ghi chú");
        string? guestEmail = null;
        if (!string.IsNullOrWhiteSpace(body.GuestEmail)) v.Email(body.GuestEmail, out guestEmail, "guestEmail");
        if (body.ExpectedTotal is not null) v.Money("expectedTotal", body.ExpectedTotal, out _, "Tổng tiền", allowZero: true);
        if (!v.IsValid) return v.Problem();

        var actor = user.GetActor();
        guestEmail ??= (await auth.GetUserAsync(actor.Id, ct)).Email; // mặc định gửi email về tài khoản đang đặt
        var id = await bookings.CreateAsync(actor, onBehalf: false, stay, new GuestInput(guestName, guestPhone!, guestEmail, note),
            BookingSource.Web, internalNote: null, body.ExpectedTotal, payment: null, ct);
        var created = await queries.GuestDetailAsync(id, ct);
        return TypedResults.Created($"/api/bookings/{created.Code}", created);
    }

    private static async Task<Results<Ok<List<BookingSummaryDto>>, ValidationProblem>> Mine(
        string? status, ClaimsPrincipal user, BookingQueryService queries, CancellationToken ct)
    {
        var v = new RequestValidator().EnumList("status", status, out BookingStatus[] statuses, "Trạng thái");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await queries.MineAsync(user.GetUserId(), statuses, ct));
    }

    private static async Task<Ok<BookingDto>> Get(
        string code, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct) =>
        TypedResults.Ok(await queries.GuestDetailAsync(await bookings.FindOwnAsync(user.GetUserId(), code, ct), ct));

    private static async Task<Ok<CancelPreviewDto>> CancelPreview(string code, ClaimsPrincipal user, BookingService bookings, CancellationToken ct) =>
        TypedResults.Ok(await bookings.CancelPreviewAsync(await bookings.FindOwnAsync(user.GetUserId(), code, ct), byStaff: false, ct));

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> Cancel(
        string code, CancelRequest? body, ClaimsPrincipal user, BookingService bookings, BookingQueryService queries, CancellationToken ct)
    {
        var v = new RequestValidator().OptionalText("reason", body?.Reason, 500, out var reason, "Lý do");
        if (!v.IsValid) return v.Problem();

        var id = await bookings.FindOwnAsync(user.GetUserId(), code, ct);
        await bookings.CancelAsync(id, user.GetActor(), byStaff: false, reason, refundOverride: null, ct);
        return TypedResults.Ok(await queries.GuestDetailAsync(id, ct));
    }

    private static async Task<Results<Ok<BookingDto>, ValidationProblem>> Lookup(LookupRequest body, BookingQueryService queries, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Text("code", body.Code, 12, out var code, "Mã đặt phòng")
            .Phone("phone", body.Phone, out var phone, required: true);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await queries.LookupAsync(code, phone!, ct));
    }

    /// <summary>Dùng chung cho khách đặt, nhân viên tạo hộ, đổi ngày.</summary>
    internal static RequestValidator ValidateStay(
        long? roomId, DateOnly? checkIn, DateOnly? checkOut, int? guests, AppClock clock, out StayInput stay, int? fallbackGuests = null)
    {
        var v = new RequestValidator()
            .StayDates(checkIn, checkOut, clock.Today, out var ci, out var co)
            .Int("guests", guests, 1, StayRules.MaxGuests, fallbackGuests, out var guestCount, "Số khách");
        if (roomId is null or <= 0) v.Add("roomId", "Vui lòng chọn phòng");
        stay = new StayInput(roomId ?? 0, ci, co, guestCount);
        return v;
    }
}
