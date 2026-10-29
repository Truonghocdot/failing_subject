using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Dữ liệu homestay đã kiểm tra ở endpoint (docs/02-nghiep-vu.md §2). Slug null → tạo mới thì tự sinh từ tên, sửa thì giữ nguyên.</summary>
public sealed record HomestayInput(
    string Name, string? Slug, string Province, string Address, string Description, string[] Amenities,
    TimeOnly CheckInTime, TimeOnly CheckOutTime, byte DepositPercent, byte HoldHours,
    byte FreeCancelDays, byte PartialRefundDays, byte PartialRefundPercent, HomestayStatus Status);

/// <summary>Dữ liệu phòng đã kiểm tra ở endpoint (docs/02-nghiep-vu.md §3).</summary>
public sealed record RoomInput(
    string Name, string Description, string? BedInfo, short? AreaM2, byte BaseGuests, byte MaxGuests, byte MinNights,
    decimal BasePrice, decimal? WeekendPrice, decimal ExtraGuestFee, decimal CleaningFee, RoomStatus Status, int SortOrder);

/// <summary>
/// Quản trị homestay, phòng, ảnh. Đã có đặt phòng thì không xoá được (409) – ẩn homestay / ngưng phòng thay vì xoá,
/// để lịch sử đặt phòng và báo cáo còn nguyên.
/// </summary>
public sealed class CatalogService(AppDbContext db, UploadStorage uploads)
{
    private const string HomestayNotFound = "Không tìm thấy homestay";
    private const string RoomNotFound = "Không tìm thấy phòng";

    public async Task<List<AdminHomestaySummaryDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.Homestays.AsNoTracking()
            .OrderBy(h => h.Name)
            .Select(h => new
            {
                h.Id, h.Slug, h.Name, h.Province, h.Status, h.UpdatedAt,
                RoomCount = h.Rooms.Count,
                ActiveRoomCount = h.Rooms.Count(r => r.Status == RoomStatus.Active),
                Cover = h.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Select(p => p.FileName).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return [.. rows.Select(h => new AdminHomestaySummaryDto(h.Id, h.Slug, h.Name, h.Province, h.Status, h.RoomCount,
            h.ActiveRoomCount, h.Cover is null ? null : UploadStorage.Url(h.Id, h.Cover), h.UpdatedAt))];
    }

    public async Task<AdminHomestayDto> GetAsync(long id, CancellationToken ct)
    {
        var h = await db.Homestays.AsNoTracking().AsSplitQuery()
            .Include(x => x.Amenities).Include(x => x.Photos).Include(x => x.Rooms)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiProblemException.NotFound(HomestayNotFound);
        return ToDto(h);
    }

    public async Task<AdminHomestayDto> CreateAsync(HomestayInput input, CancellationToken ct)
    {
        var homestay = new Homestay { Slug = "", Name = "", Province = "", Address = "" };
        await ApplyAsync(homestay, input, ct);
        db.Homestays.Add(homestay);
        await SaveHomestayAsync(ct);
        return await GetAsync(homestay.Id, ct);
    }

    public async Task<AdminHomestayDto> UpdateAsync(long id, HomestayInput input, CancellationToken ct)
    {
        var homestay = await db.Homestays.Include(h => h.Amenities).SingleOrDefaultAsync(h => h.Id == id, ct)
            ?? throw ApiProblemException.NotFound(HomestayNotFound);
        await ApplyAsync(homestay, input, ct);
        await SaveHomestayAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Chỉ xoá được homestay chưa từng có đặt phòng / khoá phòng – xoá luôn phòng, ảnh (cả file), giá riêng.</summary>
    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var homestay = await db.Homestays.SingleOrDefaultAsync(h => h.Id == id, ct)
            ?? throw ApiProblemException.NotFound(HomestayNotFound);
        if (await db.Bookings.AnyAsync(b => b.HomestayId == id, ct))
            throw ApiProblemException.Conflict("Homestay đã có đặt phòng – hãy ẩn thay vì xoá");
        if (await db.RoomBlocks.AnyAsync(b => db.Rooms.Any(r => r.Id == b.RoomId && r.HomestayId == id), ct))
            throw ApiProblemException.Conflict("Homestay có phòng đã từng bị khoá – hãy ẩn thay vì xoá");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Rooms.Where(r => r.HomestayId == id).ExecuteDeleteAsync(ct); // giá riêng xoá theo (CASCADE)
        db.Homestays.Remove(homestay); // ảnh, tiện nghi xoá theo (CASCADE)
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        uploads.DeleteHomestay(id);
    }

    // --- Ảnh ---

    public async Task<PhotoDto> AddPhotoAsync(long homestayId, byte[] content, CancellationToken ct)
    {
        if (!await db.Homestays.AnyAsync(h => h.Id == homestayId, ct)) throw ApiProblemException.NotFound(HomestayNotFound);
        if (!ImageInspector.TryInspect(content, out var extension, out var width, out var height))
            throw ApiProblemException.Validation(new Dictionary<string, string[]>
            {
                ["file"] = ["Chỉ nhận ảnh JPG, PNG hoặc WEBP (file hỏng hoặc không phải ảnh)"],
            });

        var count = await db.Photos.CountAsync(p => p.HomestayId == homestayId, ct);
        if (count >= UploadStorage.MaxPhotosPerHomestay)
            throw ApiProblemException.Conflict($"Mỗi homestay tối đa {UploadStorage.MaxPhotosPerHomestay} ảnh");
        var nextOrder = count == 0 ? 0 : await db.Photos.Where(p => p.HomestayId == homestayId).MaxAsync(p => p.SortOrder, ct) + 1;

        // Tên ngẫu nhiên, đuôi theo nội dung thật → không đoán được, không thể là script
        var photo = new Photo
        {
            HomestayId = homestayId, FileName = $"{Guid.NewGuid():N}{extension}", Width = width, Height = height, SortOrder = nextOrder,
        };
        await uploads.SaveAsync(homestayId, photo.FileName, content, ct);
        db.Photos.Add(photo);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            uploads.Delete(homestayId, photo.FileName);
            throw;
        }
        return ToDto(photo);
    }

    /// <summary>photoIds phải gồm đủ mọi ảnh của homestay; ảnh đầu tiên thành ảnh bìa.</summary>
    public async Task<List<PhotoDto>> ReorderPhotosAsync(long homestayId, long[] photoIds, CancellationToken ct)
    {
        if (!await db.Homestays.AnyAsync(h => h.Id == homestayId, ct)) throw ApiProblemException.NotFound(HomestayNotFound);
        var photos = await db.Photos.Where(p => p.HomestayId == homestayId).ToListAsync(ct);
        if (photoIds.Length != photos.Count || photoIds.Distinct().Count() != photoIds.Length || !photos.All(p => photoIds.Contains(p.Id)))
            throw ApiProblemException.Validation(new Dictionary<string, string[]>
            {
                ["photoIds"] = ["Danh sách phải gồm đủ mọi ảnh của homestay, mỗi ảnh 1 lần"],
            });

        foreach (var photo in photos) photo.SortOrder = Array.IndexOf(photoIds, photo.Id);
        await db.SaveChangesAsync(ct);
        return [.. photos.OrderBy(p => p.SortOrder).Select(ToDto)];
    }

    public async Task DeletePhotoAsync(long homestayId, long photoId, CancellationToken ct)
    {
        var photo = await db.Photos.SingleOrDefaultAsync(p => p.Id == photoId && p.HomestayId == homestayId, ct)
            ?? throw ApiProblemException.NotFound("Không tìm thấy ảnh");
        db.Photos.Remove(photo);
        await db.SaveChangesAsync(ct);
        uploads.Delete(homestayId, photo.FileName);
    }

    // --- Phòng ---

    public async Task<RoomDto> CreateRoomAsync(long homestayId, RoomInput input, CancellationToken ct)
    {
        if (!await db.Homestays.AnyAsync(h => h.Id == homestayId, ct)) throw ApiProblemException.NotFound(HomestayNotFound);
        var room = new Room { HomestayId = homestayId, Name = "" };
        Apply(room, input);
        db.Rooms.Add(room);
        await SaveRoomAsync(input.Name, ct);
        return ToDto(room);
    }

    public async Task<RoomDto> UpdateRoomAsync(long roomId, RoomInput input, CancellationToken ct)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId, ct) ?? throw ApiProblemException.NotFound(RoomNotFound);
        Apply(room, input);
        await SaveRoomAsync(input.Name, ct);
        return ToDto(room);
    }

    public async Task DeleteRoomAsync(long roomId, CancellationToken ct)
    {
        var room = await db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId, ct) ?? throw ApiProblemException.NotFound(RoomNotFound);
        if (await db.Bookings.AnyAsync(b => b.RoomId == roomId, ct) || await db.RoomBlocks.AnyAsync(b => b.RoomId == roomId, ct))
            throw ApiProblemException.Conflict("Phòng đã có đặt phòng / khoá phòng – hãy ngưng hoạt động thay vì xoá");
        db.Rooms.Remove(room);
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(Homestay h, HomestayInput input, CancellationToken ct)
    {
        if (input.Slug is not null && await db.Homestays.AnyAsync(x => x.Slug == input.Slug && x.Id != h.Id, ct))
            throw ApiProblemException.Conflict("Đường dẫn (slug) đã được homestay khác dùng", "slug");

        // Sửa tên không tự đổi slug – link đã chia sẻ vẫn chạy
        h.Slug = input.Slug ?? (h.Id == 0 ? await FreeSlugAsync(Slug.From(input.Name), ct) : h.Slug);
        h.Name = input.Name;
        h.Province = input.Province;
        h.Address = input.Address;
        h.Description = input.Description;
        h.CheckInTime = input.CheckInTime;
        h.CheckOutTime = input.CheckOutTime;
        h.DepositPercent = input.DepositPercent;
        h.HoldHours = input.HoldHours;
        h.FreeCancelDays = input.FreeCancelDays;
        h.PartialRefundDays = input.PartialRefundDays;
        h.PartialRefundPercent = input.PartialRefundPercent;
        h.Status = input.Status;

        // Đồng bộ tiện nghi: bỏ mã không còn chọn, thêm mã mới
        h.Amenities.RemoveAll(a => !input.Amenities.Contains(a.Code));
        foreach (var code in input.Amenities.Where(c => h.Amenities.All(a => a.Code != c)))
            h.Amenities.Add(new HomestayAmenity { Code = code });
    }

    /// <summary>"da-lat-pine-house" đã có → thử "da-lat-pine-house-2", "-3"...</summary>
    private async Task<string> FreeSlugAsync(string baseSlug, CancellationToken ct)
    {
        var taken = await db.Homestays.AsNoTracking()
            .Where(h => h.Slug == baseSlug || h.Slug.StartsWith(baseSlug + "-"))
            .Select(h => h.Slug)
            .ToListAsync(ct);
        if (!taken.Contains(baseSlug)) return baseSlug;
        for (var i = 2; ; i++)
            if (!taken.Contains($"{baseSlug}-{i}")) return $"{baseSlug}-{i}";
    }

    /// <summary>2 admin cùng tạo homestay trùng tên cùng lúc → unique slug chặn ở DB.</summary>
    private async Task SaveHomestayAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (Db.IsDuplicateKey(ex))
        {
            throw ApiProblemException.Conflict("Đường dẫn (slug) vừa được homestay khác dùng – thử lại hoặc đặt slug khác", "slug");
        }
    }

    private async Task SaveRoomAsync(string name, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (Db.IsDuplicateKey(ex))
        {
            throw ApiProblemException.Conflict($"Homestay đã có phòng tên \"{name}\"", "name");
        }
    }

    private static void Apply(Room r, RoomInput input)
    {
        r.Name = input.Name;
        r.Description = input.Description;
        r.BedInfo = input.BedInfo;
        r.AreaM2 = input.AreaM2;
        r.BaseGuests = input.BaseGuests;
        r.MaxGuests = input.MaxGuests;
        r.MinNights = input.MinNights;
        r.BasePrice = input.BasePrice;
        r.WeekendPrice = input.WeekendPrice;
        r.ExtraGuestFee = input.ExtraGuestFee;
        r.CleaningFee = input.CleaningFee;
        r.Status = input.Status;
        r.SortOrder = input.SortOrder;
    }

    public static string Time(TimeOnly t) => t.ToString("HH:mm");

    private static AdminHomestayDto ToDto(Homestay h) => new(
        h.Id, h.Slug, h.Name, h.Province, h.Address, h.Description, [.. h.Amenities.Select(a => a.Code).Order()],
        Time(h.CheckInTime), Time(h.CheckOutTime), h.DepositPercent, h.HoldHours,
        h.FreeCancelDays, h.PartialRefundDays, h.PartialRefundPercent, h.Status,
        [.. h.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).Select(ToDto)],
        [.. h.Rooms.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).Select(ToDto)],
        h.CreatedAt, h.UpdatedAt);

    public static PhotoDto ToDto(Photo p) => new(p.Id, UploadStorage.Url(p.HomestayId, p.FileName), p.Width, p.Height, p.SortOrder);

    public static RoomDto ToDto(Room r) => new(
        r.Id, r.HomestayId, r.Name, r.Description, r.BedInfo, r.AreaM2, r.BaseGuests, r.MaxGuests, r.MinNights,
        r.BasePrice, r.WeekendPrice, r.ExtraGuestFee, r.CleaningFee, r.Status, r.SortOrder);
}
