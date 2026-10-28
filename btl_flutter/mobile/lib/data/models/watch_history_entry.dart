import '../../core/api_client.dart';
import 'movie_summary.dart';

/// Tập xem gần nhất của một phim – khớp `WatchHistoryDto`.
class WatchHistoryEntry {
  const WatchHistoryEntry({
    required this.movie,
    required this.serverName,
    required this.episodeSlug,
    required this.episodeName,
    required this.watchedAt,
  });

  factory WatchHistoryEntry.fromJson(Json json) => WatchHistoryEntry(
    movie: MovieSummary.fromJson(json['movie'] as Json),
    serverName: json['serverName'] as String,
    episodeSlug: json['episodeSlug'] as String,
    episodeName: json['episodeName'] as String,
    watchedAt: DateTime.parse(json['watchedAt'] as String).toLocal(),
  );

  final MovieSummary movie;
  final String serverName;
  final String episodeSlug;
  final String episodeName;
  final DateTime watchedAt;

  /// "3" → "Tập 3", "Full" giữ nguyên.
  String get episodeLabel => int.tryParse(episodeName) != null ? 'Tập $episodeName' : episodeName;
}
