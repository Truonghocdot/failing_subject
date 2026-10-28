import '../../core/api_client.dart';
import 'json_helpers.dart';

/// Phim trong danh sách – khớp `MovieSummaryDto` của backend.
class MovieSummary {
  const MovieSummary({
    required this.slug,
    required this.name,
    this.originName,
    required this.posterUrl,
    required this.thumbUrl,
    this.year,
    this.rating,
  });

  factory MovieSummary.fromJson(Json json) => MovieSummary(
    slug: json['slug'] as String,
    name: json['name'] as String,
    originName: json['originName'] as String?,
    posterUrl: json['posterUrl'] as String? ?? '',
    thumbUrl: json['thumbUrl'] as String? ?? '',
    year: parseInt(json['year']),
    rating: parseDouble(json['rating']),
  );

  final String slug;
  final String name;
  final String? originName;
  final String posterUrl;
  final String thumbUrl;
  final int? year;
  final double? rating;
}
