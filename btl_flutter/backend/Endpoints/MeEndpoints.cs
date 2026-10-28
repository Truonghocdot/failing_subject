using System.Security.Claims;
using backend.Auth;
using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

/// <summary>/api/me/* – yêu cầu header Authorization: Bearer &lt;accessToken&gt;.</summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        me.MapGet("", GetMe).WithName("GetMe").WithSummary("Thông tin người dùng hiện tại");

        var favorites = me.MapGroup("/favorites");
        favorites.MapGet("", GetFavorites).WithName("GetFavorites").WithSummary("Phim yêu thích (mới nhất trước)");
        favorites.MapGet("/{slug}", GetFavoriteStatus).WithName("GetFavoriteStatus").WithSummary("Phim có trong yêu thích không");
        favorites.MapPut("/{slug}", AddFavorite).WithName("AddFavorite").WithSummary("Thêm vào yêu thích (idempotent)");
        favorites.MapDelete("/{slug}", RemoveFavorite).WithName("RemoveFavorite").WithSummary("Bỏ khỏi yêu thích");

        var history = me.MapGroup("/history");
        history.MapGet("", GetHistory).WithName("GetHistory").WithSummary("Lịch sử xem (mới nhất trước)");
        history.MapGet("/{slug}", GetHistoryEntry).WithName("GetHistoryEntry").WithSummary("Tập xem gần nhất của 1 phim (404 nếu chưa xem)");
        history.MapPut("/{slug}", SaveHistory).WithName("SaveHistory").WithSummary("Ghi tập đang xem (upsert)");
        history.MapDelete("/{slug}", RemoveHistory).WithName("RemoveHistory").WithSummary("Xoá 1 phim khỏi lịch sử");
        history.MapDelete("", ClearHistory).WithName("ClearHistory").WithSummary("Xoá toàn bộ lịch sử");

        return app;
    }

    private static async Task<Results<Ok<UserDto>, UnauthorizedHttpResult>> GetMe(
        ClaimsPrincipal user, AuthService auth, CancellationToken ct) =>
        await auth.GetUserAsync(user.GetUserId(), ct) is { } dto ? TypedResults.Ok(dto) : TypedResults.Unauthorized();

    private static async Task<Results<Ok<PagedResult<MovieSummaryDto>>, ValidationProblem>> GetFavorites(
        int? page, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await library.GetFavoritesAsync(user.GetUserId(), p, ct));
    }

    private static async Task<Results<Ok<FavoriteStatusDto>, ValidationProblem>> GetFavoriteStatus(
        string slug, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(new FavoriteStatusDto(await library.IsFavoriteAsync(user.GetUserId(), slug, ct)));
    }

    private static async Task<Results<NoContent, ValidationProblem>> AddFavorite(
        string slug, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug);
        if (!v.IsValid) return v.Problem();
        await library.AddFavoriteAsync(user.GetUserId(), slug, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem>> RemoveFavorite(
        string slug, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug);
        if (!v.IsValid) return v.Problem();
        await library.RemoveFavoriteAsync(user.GetUserId(), slug, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<PagedResult<WatchHistoryDto>>, ValidationProblem>> GetHistory(
        int? page, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await library.GetHistoryAsync(user.GetUserId(), p, ct));
    }

    private static async Task<Results<Ok<WatchHistoryDto>, NotFound, ValidationProblem>> GetHistoryEntry(
        string slug, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug);
        if (!v.IsValid) return v.Problem();
        return await library.GetHistoryEntryAsync(user.GetUserId(), slug, ct) is { } dto
            ? TypedResults.Ok(dto)
            : TypedResults.NotFound();
    }

    private static async Task<Results<NoContent, ValidationProblem>> SaveHistory(
        string slug, SaveHistoryRequest body, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator()
            .Slug(slug)
            .Text("serverName", body.ServerName, 100, out var serverName, "serverName")
            .Text("episodeSlug", body.EpisodeSlug, 50, out var episodeSlug, "episodeSlug")
            .Text("episodeName", body.EpisodeName, 50, out var episodeName, "episodeName")
            .Range("positionSeconds", body.PositionSeconds, 0, 86_400);
        if (!v.IsValid) return v.Problem();
        await library.SaveHistoryAsync(user.GetUserId(), slug, serverName, episodeSlug, episodeName, body.PositionSeconds, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ValidationProblem>> RemoveHistory(
        string slug, ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug);
        if (!v.IsValid) return v.Problem();
        await library.RemoveHistoryAsync(user.GetUserId(), slug, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ClearHistory(ClaimsPrincipal user, LibraryService library, CancellationToken ct)
    {
        await library.ClearHistoryAsync(user.GetUserId(), ct);
        return TypedResults.NoContent();
    }
}
