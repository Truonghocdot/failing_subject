using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class MovieEndpoints
{
    public static IEndpointRouteBuilder MapMovieEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/movies").WithTags("Movies").WithCacheControl(60);

        group.MapGet("", GetList)
            .WithName("GetMovies")
            .WithSummary("Danh sách phim: phim-moi-cap-nhat | phim-bo | phim-le | phim-chieu-rap");

        group.MapGet("/search", Search)
            .WithName("SearchMovies")
            .WithSummary("Tìm phim theo tên");

        group.MapGet("/{slug}", GetDetail)
            .WithName("GetMovie")
            .WithSummary("Chi tiết phim + danh sách server/tập");

        return app;
    }

    private static async Task<Results<Ok<PagedResult<MovieSummaryDto>>, ValidationProblem>> GetList(
        string? list, int? page, MovieService movies, CancellationToken ct)
    {
        var v = new RequestValidator().List(list, out var listSlug).Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await movies.GetListAsync(listSlug, p, ct));
    }

    private static async Task<Results<Ok<PagedResult<MovieSummaryDto>>, ValidationProblem>> Search(
        string? q, int? page, MovieService movies, CancellationToken ct)
    {
        var v = new RequestValidator().Keyword(q, out var keyword).Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await movies.SearchAsync(keyword, p, ct));
    }

    private static async Task<Results<Ok<MovieDetailDto>, ValidationProblem>> GetDetail(
        string slug, MovieService movies, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await movies.GetDetailAsync(slug, ct));
    }
}
