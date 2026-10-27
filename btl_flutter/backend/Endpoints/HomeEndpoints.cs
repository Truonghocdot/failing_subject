using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/home", GetHome)
            .WithTags("Home")
            .WithName("GetHome")
            .WithSummary("Trang chủ: Mới cập nhật, Phim bộ, Phim lẻ, Chiếu rạp (trang 1)")
            .WithCacheControl(60);

        return app;
    }

    private static async Task<Ok<HomeDto>> GetHome(MovieService movies, CancellationToken ct) =>
        TypedResults.Ok(await movies.GetHomeAsync(ct));
}
