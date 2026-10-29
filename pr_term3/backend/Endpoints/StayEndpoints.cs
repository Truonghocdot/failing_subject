using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>Khu khách công khai (không cần đăng nhập): tỉnh, tìm kiếm, chi tiết homestay, lịch trống, báo giá.</summary>
public static class StayEndpoints
{
    public static IEndpointRouteBuilder MapStayEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Khách – tìm & báo giá");

        api.MapGet("/provinces", Provinces).WithName("ListProvinces").WithSummary("Tỉnh / thành có homestay đang mở");
        api.MapGet("/stays", Search).WithName("SearchStays")
            .WithSummary("Tìm homestay (province, q, checkIn, checkOut, guests, amenities=wifi,pool, priceMax, sort=relevance|price|rating, page)");
        api.MapGet("/stays/{slug}", Detail).WithName("GetStay").WithSummary("Chi tiết homestay: ảnh, tiện nghi, chính sách, phòng");
        api.MapGet("/stays/{slug}/calendar", Calendar).WithName("GetStayCalendar")
            .WithSummary($"Lịch còn trống + giá từng đêm của mỗi phòng (from, to – tối đa {AvailabilityService.MaxCalendarDays} ngày)");
        api.MapGet("/stays/{slug}/reviews", Reviews).WithName("GetStayReviews").WithSummary("Đánh giá đang hiện (mới nhất trước, page)");
        api.MapPost("/quotes", Quote).WithName("Quote").WithSummary("Báo giá từng đêm, tổng, tiền cọc, còn trống không");

        return app;
    }

    private static async Task<Ok<List<ProvinceDto>>> Provinces(AvailabilityService stays, CancellationToken ct) =>
        TypedResults.Ok(await stays.ProvincesAsync(ct));

    private static async Task<Results<Ok<PagedResult<StayCardDto>>, ValidationProblem>> Search(
        string? province, string? q, DateOnly? checkIn, DateOnly? checkOut, int? guests, string? amenities, decimal? priceMax,
        string? sort, int? page, AvailabilityService stays, AppClock clock, CancellationToken ct)
    {
        var v = new RequestValidator()
            .OptionalText("province", province, 60, out var provinceValue, "Tỉnh / thành")
            .OptionalText("q", q, 100, out var term, "Từ khoá")
            .Int("guests", guests, 1, StayRules.MaxGuests, 1, out var guestCount, "Số khách")
            .Codes("amenities", amenities?.Split(','), Amenity.Codes, out var amenityCodes, "Tiện nghi")
            .OptionalMoney("priceMax", priceMax, out var max, "Giá tối đa")
            .Enum("sort", sort ?? "relevance", out StaySort order, "Sắp xếp")
            .Int("page", page, 1, 1000, 1, out var pageNumber, "Trang");
        DateOnly? ci = null, co = null;
        if (checkIn is not null || checkOut is not null)
        {
            v.StayDates(checkIn, checkOut, clock.Today, out var from, out var to);
            (ci, co) = (from, to);
        }
        if (!v.IsValid) return v.Problem();

        return TypedResults.Ok(await stays.SearchAsync(
            new StaySearch(provinceValue, term, ci, co, guestCount, amenityCodes, max, order, pageNumber), ct));
    }

    private static async Task<Ok<StayDetailDto>> Detail(string slug, AvailabilityService stays, CancellationToken ct) =>
        TypedResults.Ok(await stays.DetailAsync(slug, ct));

    private static async Task<Results<Ok<StayCalendarDto>, ValidationProblem>> Calendar(
        string slug, DateOnly? from, DateOnly? to, AvailabilityService stays, AppClock clock, CancellationToken ct)
    {
        var start = from ?? clock.Today;
        var v = new RequestValidator().DateRange(start, to ?? start.AddDays(59), AvailabilityService.MaxCalendarDays, out var f, out var t);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await stays.CalendarAsync(slug, f, t, ct));
    }

    private static async Task<Results<Ok<PagedResult<PublicReviewDto>>, ValidationProblem>> Reviews(
        string slug, int? page, ReviewService reviews, CancellationToken ct)
    {
        var v = new RequestValidator().Int("page", page, 1, 1000, 1, out var pageNumber, "Trang");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await reviews.ListPublicAsync(slug, pageNumber, ct));
    }

    private static async Task<Results<Ok<QuoteDto>, ValidationProblem>> Quote(
        QuoteRequest body, AvailabilityService stays, AppClock clock, CancellationToken ct)
    {
        var v = new RequestValidator()
            .StayDates(body.CheckIn, body.CheckOut, clock.Today, out var checkIn, out var checkOut)
            .Int("guests", body.Guests, 1, StayRules.MaxGuests, null, out var guests, "Số khách");
        if (body.RoomId is null) v.Add("roomId", "Vui lòng chọn phòng");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await stays.QuoteAsync(body.RoomId!.Value, checkIn, checkOut, guests, ct));
    }
}
