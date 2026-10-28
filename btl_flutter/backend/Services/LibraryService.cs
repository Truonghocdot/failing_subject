using backend.Contracts;
using backend.Data;
using backend.Data.Entities;
using backend.Vsmov;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

/// <summary>Yêu thích + lịch sử xem của người dùng. Thông tin phim luôn lấy từ nguồn (qua MovieService), không tin client.</summary>
public sealed class LibraryService(AppDbContext db, MovieService movies, TimeProvider time, ILogger<LibraryService> logger)
{
    public const int PageSize = 24;
    public const int MaxHistoryPerUser = 200;
    private static readonly TimeSpan MovieResyncAfter = TimeSpan.FromDays(7);

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    // ---------- Yêu thích ----------

    public async Task<PagedResult<MovieSummaryDto>> GetFavoritesAsync(Guid userId, int page, CancellationToken ct)
    {
        var query = db.Favorites.Where(f => f.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(f => new MovieSummaryDto(
                f.Movie.Slug, f.Movie.Name, f.Movie.OriginName, f.Movie.PosterUrl, f.Movie.ThumbUrl, (int?)f.Movie.Year, null))
            .ToListAsync(ct);
        return Paged(items, page, total);
    }

    public Task<bool> IsFavoriteAsync(Guid userId, string slug, CancellationToken ct) =>
        db.Favorites.AnyAsync(f => f.UserId == userId && f.Movie.Slug == slug, ct);

    public async Task AddFavoriteAsync(Guid userId, string slug, CancellationToken ct)
    {
        var movie = await EnsureMovieAsync(slug, ct);
        if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.MovieId == movie.Id, ct)) return;

        db.Favorites.Add(new Favorite { UserId = userId, MovieId = movie.Id, CreatedAt = Now });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // request song song đã thêm trước → coi như thành công (idempotent); lỗi khác thì ném lại
            db.ChangeTracker.Clear();
            if (!await db.Favorites.AnyAsync(f => f.UserId == userId && f.MovieId == movie.Id, ct)) throw;
        }
    }

    public async Task RemoveFavoriteAsync(Guid userId, string slug, CancellationToken ct)
    {
        var movieId = await MovieIdAsync(slug, ct);
        if (movieId is null) return;
        await db.Favorites.Where(f => f.UserId == userId && f.MovieId == movieId).ExecuteDeleteAsync(ct);
    }

    // ---------- Lịch sử xem ----------

    public async Task<PagedResult<WatchHistoryDto>> GetHistoryAsync(Guid userId, int page, CancellationToken ct)
    {
        var query = db.WatchHistory.Where(h => h.UserId == userId);
        var total = await query.CountAsync(ct);
        var items = await ToDto(query.OrderByDescending(h => h.WatchedAt).ThenByDescending(h => h.Id)
                .Skip((page - 1) * PageSize).Take(PageSize))
            .ToListAsync(ct);
        return Paged(items, page, total);
    }

    public Task<WatchHistoryDto?> GetHistoryEntryAsync(Guid userId, string slug, CancellationToken ct) =>
        ToDto(db.WatchHistory.Where(h => h.UserId == userId && h.Movie.Slug == slug)).SingleOrDefaultAsync(ct);

    public async Task SaveHistoryAsync(Guid userId, string slug, string serverName, string episodeSlug, string episodeName,
        int? positionSeconds, CancellationToken ct)
    {
        var movie = await EnsureMovieAsync(slug, ct);
        var entry = await db.WatchHistory.SingleOrDefaultAsync(h => h.UserId == userId && h.MovieId == movie.Id, ct);
        if (entry is null)
        {
            entry = new WatchHistory
            {
                UserId = userId,
                MovieId = movie.Id,
                ServerName = serverName,
                EpisodeSlug = episodeSlug,
                EpisodeName = episodeName,
            };
            db.WatchHistory.Add(entry);
        }
        entry.ServerName = serverName;
        entry.EpisodeSlug = episodeSlug;
        entry.EpisodeName = episodeName;
        entry.PositionSeconds = positionSeconds;
        entry.WatchedAt = Now;

        await db.SaveChangesAsync(ct);
        await TrimHistoryAsync(userId, ct);
    }

    public async Task RemoveHistoryAsync(Guid userId, string slug, CancellationToken ct)
    {
        var movieId = await MovieIdAsync(slug, ct);
        if (movieId is null) return;
        await db.WatchHistory.Where(h => h.UserId == userId && h.MovieId == movieId).ExecuteDeleteAsync(ct);
    }

    public Task ClearHistoryAsync(Guid userId, CancellationToken ct) =>
        db.WatchHistory.Where(h => h.UserId == userId).ExecuteDeleteAsync(ct);

    /// <summary>Giữ [MaxHistoryPerUser] bản ghi mới nhất.</summary>
    private async Task TrimHistoryAsync(Guid userId, CancellationToken ct)
    {
        var stale = await db.WatchHistory.Where(h => h.UserId == userId)
            .OrderByDescending(h => h.WatchedAt).ThenByDescending(h => h.Id)
            .Skip(MaxHistoryPerUser)
            .Select(h => h.Id)
            .ToListAsync(ct);
        if (stale.Count > 0) await db.WatchHistory.Where(h => stale.Contains(h.Id)).ExecuteDeleteAsync(ct);
    }

    // ---------- Phim ----------

    /// <summary>Lấy phim trong DB; chưa có hoặc quá 7 ngày thì đồng bộ từ nguồn. Nguồn lỗi mà DB đã có → dùng bản cũ.</summary>
    private async Task<Movie> EnsureMovieAsync(string slug, CancellationToken ct)
    {
        var movie = await db.Movies.SingleOrDefaultAsync(m => m.Slug == slug, ct);
        if (movie is not null && Now - movie.SyncedAt < MovieResyncAfter) return movie;

        MovieDetailDto detail;
        try
        {
            detail = await movies.GetDetailAsync(slug, ct);
        }
        catch (VsmovUnavailableException ex) when (movie is not null)
        {
            logger.LogWarning(ex, "Không đồng bộ được phim {Slug}, dùng dữ liệu cũ", slug);
            return movie;
        }

        // Slug đổi trên nguồn nhưng cùng _id → cập nhật bản ghi cũ thay vì tạo trùng
        movie ??= detail.SourceId is { } sourceId ? await db.Movies.SingleOrDefaultAsync(m => m.SourceId == sourceId, ct) : null;
        var isNew = movie is null;
        movie ??= db.Movies.Add(new Movie { Slug = slug, Name = detail.Name, PosterUrl = "", ThumbUrl = "" }).Entity;

        movie.SourceId = detail.SourceId;
        movie.Slug = detail.Slug;
        movie.Name = Truncate(detail.Name, 255);
        movie.OriginName = detail.OriginName is null ? null : Truncate(detail.OriginName, 255);
        movie.PosterUrl = Truncate(detail.PosterUrl, 500);
        movie.ThumbUrl = Truncate(detail.ThumbUrl, 500);
        movie.Year = detail.Year is { } year and > 0 and < short.MaxValue ? (short)year : null;
        movie.Type = Truncate(detail.Type, 20);
        movie.SyncedAt = Now;

        try
        {
            await db.SaveChangesAsync(ct);
            return movie;
        }
        catch (DbUpdateException) when (isNew)
        {
            // request song song vừa thêm cùng phim → bỏ bản của mình, đọc lại
            db.Entry(movie).State = EntityState.Detached;
            return await db.Movies.SingleAsync(m => m.Slug == detail.Slug, ct);
        }
    }

    private Task<long?> MovieIdAsync(string slug, CancellationToken ct) =>
        db.Movies.Where(m => m.Slug == slug).Select(m => (long?)m.Id).SingleOrDefaultAsync(ct);

    private static IQueryable<WatchHistoryDto> ToDto(IQueryable<WatchHistory> query) =>
        query.Select(h => new WatchHistoryDto(
            new MovieSummaryDto(h.Movie.Slug, h.Movie.Name, h.Movie.OriginName, h.Movie.PosterUrl, h.Movie.ThumbUrl, (int?)h.Movie.Year, null),
            h.ServerName, h.EpisodeSlug, h.EpisodeName, h.PositionSeconds, h.WatchedAt));

    private static PagedResult<T> Paged<T>(IReadOnlyList<T> items, int page, int total) =>
        new(items, page, PageSize, total, (total + PageSize - 1) / PageSize);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
