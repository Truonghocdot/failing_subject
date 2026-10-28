import '../../core/api_client.dart';
import 'json_helpers.dart';
import 'movie_summary.dart';

/// Một hàng phim ở trang chủ (key = slug danh sách: phim-moi-cap-nhat, phim-bo, ...).
class HomeSection {
  const HomeSection({required this.key, required this.title, required this.items});

  factory HomeSection.fromJson(Json json) => HomeSection(
    key: json['key'] as String,
    title: json['title'] as String,
    items: parseList(json['items'], MovieSummary.fromJson),
  );

  final String key;
  final String title;
  final List<MovieSummary> items;
}
