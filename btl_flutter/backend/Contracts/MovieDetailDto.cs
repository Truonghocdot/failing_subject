namespace backend.Contracts;

public sealed record TaxonomyDto(int Id, string Name, string Slug);

public sealed record EpisodeDto(string Name, string Slug, string EmbedUrl);

public sealed record ServerDto(string Name, IReadOnlyList<EpisodeDto> Episodes);

public sealed record MovieDetailDto(
    string Slug,
    string Name,
    string? OriginName,
    string Content,
    string Type,            // series | single | ...
    string Status,          // ongoing | completed | ...
    string PosterUrl,
    string ThumbUrl,
    string? TrailerUrl,
    string? Duration,       // "60 phút"
    string? EpisodeCurrent,
    string? EpisodeTotal,
    string? Quality,
    string? Lang,
    string? Showtimes,
    int? Year,
    double? Rating,
    IReadOnlyList<string> Actors,
    IReadOnlyList<string> Directors,
    IReadOnlyList<TaxonomyDto> Genres,
    IReadOnlyList<TaxonomyDto> Countries,
    IReadOnlyList<ServerDto> Servers);
