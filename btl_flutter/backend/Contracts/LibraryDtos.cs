namespace backend.Contracts;

public sealed record FavoriteStatusDto(bool IsFavorite);

public sealed record SaveHistoryRequest(string? ServerName, string? EpisodeSlug, string? EpisodeName, int? PositionSeconds = null);

public sealed record WatchHistoryDto(
    MovieSummaryDto Movie,
    string ServerName,
    string EpisodeSlug,
    string EpisodeName,
    int? PositionSeconds,
    DateTime WatchedAt);
