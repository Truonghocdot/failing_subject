import '../../core/api_client.dart';
import 'json_helpers.dart';
import 'taxonomy.dart';

class Episode {
  const Episode({required this.name, required this.slug, required this.embedUrl});

  factory Episode.fromJson(Json json) =>
      Episode(name: json['name'] as String, slug: json['slug'] as String, embedUrl: json['embedUrl'] as String);

  final String name;
  final String slug;
  final String embedUrl;

  /// "1" → "Tập 1", "Full" giữ nguyên.
  String get label => int.tryParse(name) != null ? 'Tập $name' : name;
}

class Server {
  const Server({required this.name, required this.episodes});

  factory Server.fromJson(Json json) =>
      Server(name: json['name'] as String, episodes: parseList(json['episodes'], Episode.fromJson));

  final String name;
  final List<Episode> episodes;
}

/// Chi tiết phim – khớp `MovieDetailDto` của backend.
class MovieDetail {
  const MovieDetail({
    required this.slug,
    required this.name,
    this.originName,
    required this.content,
    required this.type,
    required this.status,
    required this.posterUrl,
    required this.thumbUrl,
    this.trailerUrl,
    this.duration,
    this.episodeCurrent,
    this.episodeTotal,
    this.quality,
    this.lang,
    this.showtimes,
    this.year,
    this.rating,
    this.actors = const [],
    this.directors = const [],
    this.genres = const [],
    this.countries = const [],
    this.servers = const [],
  });

  factory MovieDetail.fromJson(Json json) => MovieDetail(
    slug: json['slug'] as String,
    name: json['name'] as String,
    originName: json['originName'] as String?,
    content: json['content'] as String? ?? '',
    type: json['type'] as String? ?? 'unknown',
    status: json['status'] as String? ?? 'unknown',
    posterUrl: json['posterUrl'] as String? ?? '',
    thumbUrl: json['thumbUrl'] as String? ?? '',
    trailerUrl: json['trailerUrl'] as String?,
    duration: json['duration'] as String?,
    episodeCurrent: json['episodeCurrent'] as String?,
    episodeTotal: json['episodeTotal'] as String?,
    quality: json['quality'] as String?,
    lang: json['lang'] as String?,
    showtimes: json['showtimes'] as String?,
    year: parseInt(json['year']),
    rating: parseDouble(json['rating']),
    actors: parseStrings(json['actors']),
    directors: parseStrings(json['directors']),
    genres: parseList(json['genres'], Taxonomy.fromJson),
    countries: parseList(json['countries'], Taxonomy.fromJson),
    servers: parseList(json['servers'], Server.fromJson),
  );

  final String slug;
  final String name;
  final String? originName;
  final String content;
  final String type;
  final String status;
  final String posterUrl;
  final String thumbUrl;
  final String? trailerUrl;
  final String? duration;
  final String? episodeCurrent;
  final String? episodeTotal;
  final String? quality;
  final String? lang;
  final String? showtimes;
  final int? year;
  final double? rating;
  final List<String> actors;
  final List<String> directors;
  final List<Taxonomy> genres;
  final List<Taxonomy> countries;
  final List<Server> servers;

  bool get hasEpisodes => servers.any((s) => s.episodes.isNotEmpty);

  /// Phim bộ hoặc có nhiều tập → hiển thị lưới tập; phim lẻ chỉ cần nút "Xem phim".
  bool get showsEpisodeGrid => type == 'series' || servers.any((s) => s.episodes.length > 1);

  String? get statusLabel => switch (status) {
    'ongoing' => 'Đang chiếu',
    'completed' => 'Hoàn tất',
    'trailer' => 'Sắp chiếu',
    _ => null,
  };
}
