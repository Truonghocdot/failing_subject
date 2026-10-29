using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Đánh giá (docs/02-nghiep-vu.md §10): khách sở hữu đặt phòng đã trả phòng ≤ 30 ngày, 1 lần / đặt phòng; admin ẩn / hiện.</summary>
public sealed class ReviewService(AppDbContext db, AppClock clock)
{
    public const int WindowDays = 30;
    public const int PageSize = 10;

    /// <summary>null = được đánh giá; khác null = lý do không được (hiện cho khách).</summary>
    public static string? CannotReview(Booking b, bool alreadyReviewed, DateTime nowUtc)
    {
        if (alreadyReviewed) return "Bạn đã đánh giá đặt phòng này";
        if (b.Status != BookingStatus.CheckedOut || b.CheckedOutAt is null) return "Chỉ đánh giá được sau khi trả phòng";
        if (nowUtc - b.CheckedOutAt.Value > TimeSpan.FromDays(WindowDays)) return $"Chỉ đánh giá trong {WindowDays} ngày sau khi trả phòng";
        return null;
    }

    public async Task<ReviewDto> CreateAsync(long bookingId, Guid userId, byte rating, string? comment, CancellationToken ct)
    {
        var b = await db.Bookings.AsNoTracking().SingleAsync(x => x.Id == bookingId, ct);
        var exists = await db.Reviews.AnyAsync(r => r.BookingId == bookingId, ct);
        if (CannotReview(b, exists, clock.UtcNow) is { } reason) throw ApiProblemException.Conflict(reason);

        var review = new Review { BookingId = b.Id, HomestayId = b.HomestayId, UserId = userId, Rating = rating, Comment = comment };
        db.Reviews.Add(review);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (Db.IsDuplicateKey(ex))
        {
            throw ApiProblemException.Conflict("Bạn đã đánh giá đặt phòng này"); // 2 request gửi đồng thời
        }
        return new ReviewDto(review.Id, review.Rating, review.Comment, review.CreatedAt);
    }

    /// <summary>Đánh giá đang hiện của homestay – tên người viết rút gọn ("Minh T."), không lộ email / SĐT.</summary>
    public async Task<PagedResult<PublicReviewDto>> ListPublicAsync(string slug, int page, CancellationToken ct)
    {
        var homestayId = await db.Homestays.Where(h => h.Slug == slug && h.Status == HomestayStatus.Active).Select(h => (long?)h.Id).SingleOrDefaultAsync(ct)
                         ?? throw ApiProblemException.NotFound("Không tìm thấy homestay");
        var query = db.Reviews.AsNoTracking().Where(r => r.HomestayId == homestayId && !r.IsHidden);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Join(db.Bookings, r => r.BookingId, b => b.Id, (r, b) => new { r, b.CheckIn, RoomName = b.Room.Name })
            .Join(db.Users, x => x.r.UserId, u => u.Id, (x, u) => new { x.r, x.CheckIn, x.RoomName, u.FullName })
            .ToListAsync(ct);
        return new PagedResult<PublicReviewDto>(
            [.. rows.Select(x => new PublicReviewDto(x.r.Id, x.r.Rating, x.r.Comment, ShortName(x.FullName), x.RoomName, x.CheckIn, x.r.CreatedAt))],
            page, PageSize, total);
    }

    public async Task<PagedResult<AdminReviewDto>> ListAdminAsync(long? homestayId, bool? hidden, int page, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking();
        if (homestayId is { } id) query = query.Where(r => r.HomestayId == id);
        if (hidden is { } h) query = query.Where(r => r.IsHidden == h);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Join(db.Bookings, r => r.BookingId, b => b.Id, (r, b) => new { r, b.Code, HomestayName = b.Homestay.Name, b.GuestName })
            .ToListAsync(ct);
        return new PagedResult<AdminReviewDto>(
            [.. rows.Select(x => new AdminReviewDto(x.r.Id, x.r.BookingId, x.Code, x.r.HomestayId, x.HomestayName, x.GuestName, x.r.Rating, x.r.Comment, x.r.IsHidden, x.r.CreatedAt))],
            page, PageSize, total);
    }

    public async Task SetHiddenAsync(long id, bool hidden, CancellationToken ct)
    {
        var updated = await db.Reviews.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsHidden, hidden), ct);
        if (updated == 0) throw ApiProblemException.NotFound("Không tìm thấy đánh giá");
    }

    /// <summary>"Trần Thị Minh" → "Minh T." (tên gọi là từ cuối, họ viết tắt).</summary>
    public static string ShortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Khách",
            1 => parts[0],
            _ => $"{parts[^1]} {char.ToUpperInvariant(parts[0][0])}.",
        };
    }
}
