using backend.Auth;
using backend.Contracts;
using backend.Data.Entities;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace backend.Endpoints;

/// <summary>
/// /api/admin – homestay, phòng, ảnh, giá theo ngày. Đọc danh sách: lễ tân + quản trị (lễ tân cần để lọc lịch, tạo đặt phòng);
/// thêm / sửa / xoá: chỉ quản trị.
/// </summary>
public static class AdminCatalogEndpoints
{
    public static RouteGroupBuilder MapAdminCatalog(this RouteGroupBuilder admin)
    {
        var homestays = admin.MapGroup("/homestays").WithTags("Quản trị – homestay & phòng");
        homestays.MapGet("", List).WithName("AdminListHomestays").WithSummary("Mọi homestay (cả đang ẩn)");
        homestays.MapGet("/{id:long}", Get).WithName("AdminGetHomestay").WithSummary("Chi tiết: chính sách, ảnh, mọi phòng");

        var manage = admin.MapGroup("").WithTags("Quản trị – homestay & phòng").RequireAuthorization(Roles.AdminPolicy);
        manage.MapPost("/homestays", Create).WithName("CreateHomestay").WithSummary("Tạo homestay (slug tự sinh từ tên nếu bỏ trống)");
        manage.MapPut("/homestays/{id:long}", Update).WithName("UpdateHomestay").WithSummary("Sửa thông tin, chính sách, ẩn / hiện");
        manage.MapDelete("/homestays/{id:long}", Delete).WithName("DeleteHomestay").WithSummary("Xoá – chỉ khi chưa có đặt phòng (409)");

        manage.MapPost("/homestays/{id:long}/photos", UploadPhoto).WithName("UploadPhoto")
            .WithSummary("Tải 1 ảnh (multipart, field \"file\"): JPG / PNG / WEBP ≤ 5 MB, tối đa 20 ảnh")
            .DisableAntiforgery() // API dùng Bearer token, không dùng cookie đăng nhập → không cần chống CSRF
            .WithMetadata(new RequestSizeLimitAttribute(UploadStorage.MaxBytes + 64 * 1024));
        manage.MapPut("/homestays/{id:long}/photos/order", ReorderPhotos).WithName("ReorderPhotos")
            .WithSummary("Sắp xếp ảnh – ảnh đầu tiên là ảnh bìa");
        manage.MapDelete("/homestays/{id:long}/photos/{photoId:long}", DeletePhoto).WithName("DeletePhoto").WithSummary("Xoá ảnh");

        manage.MapPost("/homestays/{id:long}/rooms", CreateRoom).WithName("CreateRoom").WithSummary("Thêm phòng");
        manage.MapPut("/rooms/{roomId:long}", UpdateRoom).WithName("UpdateRoom").WithSummary("Sửa phòng, ngưng / mở lại");
        manage.MapDelete("/rooms/{roomId:long}", DeleteRoom).WithName("DeleteRoom")
            .WithSummary("Xoá – chỉ khi chưa từng có đặt phòng / khoá phòng (409)");

        manage.MapGet("/rates", ListRates).WithName("ListRates").WithSummary("Giá thực tế từng đêm (roomIds=1,2, from, to)");
        manage.MapPut("/rates", SetRates).WithName("SetRates").WithSummary("Đặt giá riêng cho khoảng ngày, nhiều phòng");
        manage.MapDelete("/rates", ClearRates).WithName("ClearRates").WithSummary("Bỏ giá riêng trong khoảng ngày");

        return admin;
    }

    private static async Task<Ok<List<AdminHomestaySummaryDto>>> List(CatalogService catalog, CancellationToken ct) =>
        TypedResults.Ok(await catalog.ListAsync(ct));

    private static async Task<Ok<AdminHomestayDto>> Get(long id, CatalogService catalog, CancellationToken ct) =>
        TypedResults.Ok(await catalog.GetAsync(id, ct));

    private static async Task<Results<Created<AdminHomestayDto>, ValidationProblem>> Create(
        HomestayRequest body, CatalogService catalog, CancellationToken ct)
    {
        var v = Validate(body, out var input);
        if (!v.IsValid) return v.Problem();
        var created = await catalog.CreateAsync(input, ct);
        return TypedResults.Created($"/api/admin/homestays/{created.Id}", created);
    }

    private static async Task<Results<Ok<AdminHomestayDto>, ValidationProblem>> Update(
        long id, HomestayRequest body, CatalogService catalog, CancellationToken ct)
    {
        var v = Validate(body, out var input);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await catalog.UpdateAsync(id, input, ct));
    }

    private static async Task<NoContent> Delete(long id, CatalogService catalog, CancellationToken ct)
    {
        await catalog.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Created<PhotoDto>, ValidationProblem>> UploadPhoto(
        long id, IFormFile? file, CatalogService catalog, CancellationToken ct)
    {
        var v = new RequestValidator();
        if (file is null || file.Length == 0) v.Add("file", "Vui lòng chọn ảnh");
        else if (file.Length > UploadStorage.MaxBytes) v.Add("file", $"Ảnh tối đa {UploadStorage.MaxBytes / 1024 / 1024} MB");
        if (!v.IsValid) return v.Problem();

        using var buffer = new MemoryStream((int)file!.Length);
        await file.CopyToAsync(buffer, ct);
        var photo = await catalog.AddPhotoAsync(id, buffer.ToArray(), ct);
        return TypedResults.Created(photo.Url, photo);
    }

    private static async Task<Results<Ok<List<PhotoDto>>, ValidationProblem>> ReorderPhotos(
        long id, PhotoOrderRequest body, CatalogService catalog, CancellationToken ct)
    {
        if (body.PhotoIds is null) return new RequestValidator().Add("photoIds", "Vui lòng gửi thứ tự ảnh").Problem();
        return TypedResults.Ok(await catalog.ReorderPhotosAsync(id, body.PhotoIds, ct));
    }

    private static async Task<NoContent> DeletePhoto(long id, long photoId, CatalogService catalog, CancellationToken ct)
    {
        await catalog.DeletePhotoAsync(id, photoId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Created<RoomDto>, ValidationProblem>> CreateRoom(
        long id, RoomRequest body, CatalogService catalog, CancellationToken ct)
    {
        var v = Validate(body, out var input);
        if (!v.IsValid) return v.Problem();
        var room = await catalog.CreateRoomAsync(id, input, ct);
        return TypedResults.Created($"/api/admin/rooms/{room.Id}", room);
    }

    private static async Task<Results<Ok<RoomDto>, ValidationProblem>> UpdateRoom(
        long roomId, RoomRequest body, CatalogService catalog, CancellationToken ct)
    {
        var v = Validate(body, out var input);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await catalog.UpdateRoomAsync(roomId, input, ct));
    }

    private static async Task<NoContent> DeleteRoom(long roomId, CatalogService catalog, CancellationToken ct)
    {
        await catalog.DeleteRoomAsync(roomId, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<List<RoomRatesDto>>, ValidationProblem>> ListRates(
        string? roomIds, DateOnly? from, DateOnly? to, PricingService pricing, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Ids("roomIds", RequestValidator.ParseIds(roomIds), PricingService.MaxRoomsPerChange, out var ids, "Phòng")
            .DateRange(from, to, PricingService.MaxRangeDays, out var f, out var t);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await pricing.ListAsync(ids, f, t, ct));
    }

    private static async Task<Results<Ok<RatesChangedDto>, ValidationProblem>> SetRates(
        RateRequest body, PricingService pricing, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Ids("roomIds", body.RoomIds, PricingService.MaxRoomsPerChange, out var ids, "Phòng")
            .DateRange(body.From, body.To, PricingService.MaxRangeDays, out var f, out var t)
            .Money("price", body.Price, out var price, "Giá")
            .OptionalText("note", body.Note, 100, out var note, "Ghi chú");
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await pricing.SetAsync(ids, f, t, price, note, ct));
    }

    private static async Task<Results<Ok<RatesChangedDto>, ValidationProblem>> ClearRates(
        string? roomIds, DateOnly? from, DateOnly? to, PricingService pricing, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Ids("roomIds", RequestValidator.ParseIds(roomIds), PricingService.MaxRoomsPerChange, out var ids, "Phòng")
            .DateRange(from, to, PricingService.MaxRangeDays, out var f, out var t);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await pricing.ClearAsync(ids, f, t, ct));
    }

    private static RequestValidator Validate(HomestayRequest b, out HomestayInput input)
    {
        var v = new RequestValidator()
            .Text("name", b.Name, 120, out var name, "Tên homestay")
            .Text("province", b.Province, 60, out var province, "Tỉnh / thành")
            .Text("address", b.Address, 255, out var address, "Địa chỉ")
            .OptionalText("description", b.Description, 5000, out var description, "Mô tả")
            .Codes("amenities", b.Amenities, Amenity.Codes, out var amenities, "Tiện nghi")
            .TimeOfDay("checkInTime", b.CheckInTime, new TimeOnly(14, 0), out var checkIn, "Giờ nhận phòng")
            .TimeOfDay("checkOutTime", b.CheckOutTime, new TimeOnly(12, 0), out var checkOut, "Giờ trả phòng")
            .Int("depositPercent", b.DepositPercent, 0, 100, 30, out var deposit, "Tỉ lệ cọc (%)")
            .Int("holdHours", b.HoldHours, 1, 72, 24, out var hold, "Số giờ giữ chỗ")
            .Int("freeCancelDays", b.FreeCancelDays, 0, 90, 7, out var freeDays, "Số ngày huỷ miễn phí")
            .Int("partialRefundDays", b.PartialRefundDays, 0, 90, 3, out var partialDays, "Số ngày hoàn một phần")
            .Int("partialRefundPercent", b.PartialRefundPercent, 0, 100, 50, out var partialPercent, "Tỉ lệ hoàn một phần (%)")
            .Enum("status", b.Status ?? "active", out HomestayStatus status, "Trạng thái");
        if (partialDays > freeDays) v.Add("partialRefundDays", "Số ngày hoàn một phần phải ≤ số ngày huỷ miễn phí");

        string? slug = null;
        if (!string.IsNullOrWhiteSpace(b.Slug))
        {
            slug = b.Slug.Trim().ToLowerInvariant();
            if (!Slug.IsValid(slug)) v.Add("slug", "Slug chỉ gồm a-z, 0-9 và dấu gạch ngang (VD da-lat-pine-house)");
        }

        input = new HomestayInput(name, slug, province, address, description ?? "", amenities, checkIn, checkOut,
            (byte)deposit, (byte)hold, (byte)freeDays, (byte)partialDays, (byte)partialPercent, status);
        return v;
    }

    private static RequestValidator Validate(RoomRequest b, out RoomInput input)
    {
        var v = new RequestValidator()
            .Text("name", b.Name, 80, out var name, "Tên phòng")
            .OptionalText("description", b.Description, 5000, out var description, "Mô tả")
            .OptionalText("bedInfo", b.BedInfo, 120, out var bedInfo, "Thông tin giường")
            .Int("baseGuests", b.BaseGuests, 1, StayRules.MaxGuests, 2, out var baseGuests, "Số khách tiêu chuẩn")
            .Int("maxGuests", b.MaxGuests, 1, StayRules.MaxGuests, b.BaseGuests ?? 2, out var maxGuests, "Số khách tối đa")
            .Int("minNights", b.MinNights, 1, StayRules.MaxNights, 1, out var minNights, "Số đêm tối thiểu")
            .Money("basePrice", b.BasePrice, out var basePrice, "Giá cơ bản")
            .OptionalMoney("weekendPrice", b.WeekendPrice, out var weekendPrice, "Giá cuối tuần")
            .Money("extraGuestFee", b.ExtraGuestFee, out var extraGuestFee, "Phụ thu khách thêm", allowZero: true, fallback: 0)
            .Money("cleaningFee", b.CleaningFee, out var cleaningFee, "Phí dọn phòng", allowZero: true, fallback: 0)
            .Enum("status", b.Status ?? "active", out RoomStatus status, "Trạng thái")
            .Int("sortOrder", b.SortOrder, 0, 1000, 0, out var sortOrder, "Thứ tự");
        if (b.AreaM2 is not null) v.Int("areaM2", b.AreaM2, 1, 10_000, null, out _, "Diện tích (m²)");
        if (maxGuests < baseGuests) v.Add("maxGuests", "Số khách tối đa phải ≥ số khách tiêu chuẩn");

        input = new RoomInput(name, description ?? "", bedInfo, (short?)b.AreaM2, (byte)baseGuests, (byte)maxGuests, (byte)minNights,
            basePrice, weekendPrice, extraGuestFee, cleaningFee, status, sortOrder);
        return v;
    }
}
