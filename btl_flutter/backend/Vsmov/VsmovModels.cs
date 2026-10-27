using System.Text.Json.Serialization;

namespace backend.Vsmov;

// Model JSON thô của vsmov (snake_case). Mọi trường đều nullable vì nguồn không ổn định.
// Tài liệu: docs/02-api-nguon-vsmov.md – mẫu: docs/samples/*.json

/// <summary>/danh-sach/*, /tim-kiem, /the-loai/{slug}, /quoc-gia/{slug}, /nam/{year}</summary>
public sealed class VsmovListResponse
{
    [JsonPropertyName("items")] public List<VsmovListItem>? Items { get; init; }
    [JsonPropertyName("pathImage")] public string? PathImage { get; init; }
    [JsonPropertyName("pagination")] public VsmovPagination? Pagination { get; init; }
}

public sealed class VsmovListItem
{
    [JsonPropertyName("_id")] public int? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("origin_name")] public string? OriginName { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("poster_url")] public string? PosterUrl { get; init; }
    [JsonPropertyName("thumb_url")] public string? ThumbUrl { get; init; }
    [JsonPropertyName("year")] public int? Year { get; init; }
    [JsonPropertyName("tmdb")] public VsmovTmdb? Tmdb { get; init; }
}

public sealed class VsmovPagination
{
    [JsonPropertyName("totalItems")] public int? TotalItems { get; init; }
    [JsonPropertyName("totalItemsPerPage")] public int? TotalItemsPerPage { get; init; }
    [JsonPropertyName("currentPage")] public int? CurrentPage { get; init; }
    [JsonPropertyName("totalPages")] public int? TotalPages { get; init; }
}

public sealed class VsmovTmdb
{
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("vote_average")] public double? VoteAverage { get; init; }
    [JsonPropertyName("vote_count")] public int? VoteCount { get; init; }
}

/// <summary>/phim/{slug}</summary>
public sealed class VsmovDetailResponse
{
    [JsonPropertyName("movie")] public VsmovMovie? Movie { get; init; }
    [JsonPropertyName("episodes")] public List<VsmovServer>? Episodes { get; init; }
}

public sealed class VsmovMovie
{
    [JsonPropertyName("_id")] public int? Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("origin_name")] public string? OriginName { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("poster_url")] public string? PosterUrl { get; init; }
    [JsonPropertyName("thumb_url")] public string? ThumbUrl { get; init; }
    [JsonPropertyName("trailer_url")] public string? TrailerUrl { get; init; }
    [JsonPropertyName("time")] public string? Time { get; init; }
    [JsonPropertyName("episode_current")] public string? EpisodeCurrent { get; init; }
    [JsonPropertyName("episode_total")] public string? EpisodeTotal { get; init; }
    [JsonPropertyName("quality")] public string? Quality { get; init; }
    [JsonPropertyName("lang")] public string? Lang { get; init; }
    [JsonPropertyName("showtimes")] public string? Showtimes { get; init; }
    [JsonPropertyName("year")] public int? Year { get; init; }
    [JsonPropertyName("actor")] public List<string?>? Actor { get; init; }
    [JsonPropertyName("director")] public List<string?>? Director { get; init; }
    [JsonPropertyName("category")] public List<VsmovTaxonomy>? Category { get; init; }
    [JsonPropertyName("country")] public List<VsmovTaxonomy>? Country { get; init; }
    [JsonPropertyName("tmdb")] public VsmovTmdb? Tmdb { get; init; }
}

public sealed class VsmovServer
{
    [JsonPropertyName("server_name")] public string? ServerName { get; init; }
    [JsonPropertyName("server_data")] public List<VsmovEpisode>? ServerData { get; init; }
}

public sealed class VsmovEpisode
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
    [JsonPropertyName("link_embed")] public string? LinkEmbed { get; init; }
}

/// <summary>/the-loai, /quoc-gia – dữ liệu nằm trong data.items</summary>
public sealed class VsmovTaxonomyResponse
{
    [JsonPropertyName("data")] public VsmovTaxonomyData? Data { get; init; }
}

public sealed class VsmovTaxonomyData
{
    [JsonPropertyName("items")] public List<VsmovTaxonomy>? Items { get; init; }
}

/// <summary>Trong chi tiết phim dùng "id", trong danh sách thể loại/quốc gia dùng "_id".</summary>
public sealed class VsmovTaxonomy
{
    [JsonPropertyName("id")] public int? Id { get; init; }
    [JsonPropertyName("_id")] public int? LegacyId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("slug")] public string? Slug { get; init; }
}
