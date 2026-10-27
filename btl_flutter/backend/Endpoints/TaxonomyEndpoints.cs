using backend.Contracts;
using backend.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace backend.Endpoints;

public static class TaxonomyEndpoints
{
    public static IEndpointRouteBuilder MapTaxonomyEndpoints(this IEndpointRouteBuilder app)
    {
        var genres = app.MapGroup("/api/genres").WithTags("Genres");
        genres.MapGet("", GetGenres).WithName("GetGenres").WithSummary("Danh sách thể loại").WithCacheControl(3600);
        genres.MapGet("/{slug}/movies", GetByGenre).WithName("GetMoviesByGenre").WithSummary("Phim theo thể loại").WithCacheControl(60);

        var countries = app.MapGroup("/api/countries").WithTags("Countries");
        countries.MapGet("", GetCountries).WithName("GetCountries").WithSummary("Danh sách quốc gia").WithCacheControl(3600);
        countries.MapGet("/{slug}/movies", GetByCountry).WithName("GetMoviesByCountry").WithSummary("Phim theo quốc gia").WithCacheControl(60);

        app.MapGet("/api/years/{year:int}/movies", GetByYear)
            .WithTags("Years")
            .WithName("GetMoviesByYear")
            .WithSummary("Phim theo năm phát hành")
            .WithCacheControl(60);

        return app;
    }

    private static async Task<Ok<IReadOnlyList<TaxonomyDto>>> GetGenres(MovieService movies, CancellationToken ct) =>
        TypedResults.Ok(await movies.GetGenresAsync(ct));

    private static async Task<Ok<IReadOnlyList<TaxonomyDto>>> GetCountries(MovieService movies, CancellationToken ct) =>
        TypedResults.Ok(await movies.GetCountriesAsync(ct));

    private static async Task<Results<Ok<PagedResult<MovieSummaryDto>>, ValidationProblem>> GetByGenre(
        string slug, int? page, MovieService movies, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug).Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await movies.GetByGenreAsync(slug, p, ct));
    }

    private static async Task<Results<Ok<PagedResult<MovieSummaryDto>>, ValidationProblem>> GetByCountry(
        string slug, int? page, MovieService movies, CancellationToken ct)
    {
        var v = new RequestValidator().Slug(slug).Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await movies.GetByCountryAsync(slug, p, ct));
    }

    private static async Task<Results<Ok<PagedResult<MovieSummaryDto>>, ValidationProblem>> GetByYear(
        int year, int? page, MovieService movies, CancellationToken ct)
    {
        var v = new RequestValidator().Year(year).Page(page, out var p);
        if (!v.IsValid) return v.Problem();
        return TypedResults.Ok(await movies.GetByYearAsync(year, p, ct));
    }
}
