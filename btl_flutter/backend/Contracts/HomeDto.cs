namespace backend.Contracts;

public sealed record HomeSectionDto(string Key, string Title, IReadOnlyList<MovieSummaryDto> Items);

public sealed record HomeDto(IReadOnlyList<HomeSectionDto> Sections);
