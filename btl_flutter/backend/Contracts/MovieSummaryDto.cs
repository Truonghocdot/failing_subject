namespace backend.Contracts;

/// <summary>Phim trong danh sách / kết quả tìm kiếm.</summary>
public sealed record MovieSummaryDto(
    string Slug,
    string Name,
    string? OriginName,
    string PosterUrl,
    string ThumbUrl,
    int? Year,
    double? Rating);
