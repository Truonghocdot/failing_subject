import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import 'models/movie_summary.dart';
import 'models/paged_result.dart';
import 'models/watch_history_entry.dart';

final libraryRepositoryProvider = Provider<LibraryRepository>((ref) => LibraryRepository(ref.watch(apiClientProvider)));

/// /api/me/favorites, /api/me/history – cần đăng nhập.
class LibraryRepository {
  LibraryRepository(this._api);

  final ApiClient _api;

  static String _slug(String slug) => Uri.encodeComponent(slug);

  Future<PagedResult<MovieSummary>> getFavorites({int page = 1}) => _api.get(
    '/api/me/favorites',
    query: {'page': page},
    parse: (json) => PagedResult.fromJson(json as Json, MovieSummary.fromJson),
  );

  Future<bool> isFavorite(String slug) =>
      _api.get('/api/me/favorites/${_slug(slug)}', parse: (json) => (json as Json)['isFavorite'] as bool);

  Future<void> addFavorite(String slug) => _api.put('/api/me/favorites/${_slug(slug)}');

  Future<void> removeFavorite(String slug) => _api.delete('/api/me/favorites/${_slug(slug)}');

  Future<PagedResult<WatchHistoryEntry>> getHistory({int page = 1}) => _api.get(
    '/api/me/history',
    query: {'page': page},
    parse: (json) => PagedResult.fromJson(json as Json, WatchHistoryEntry.fromJson),
  );

  /// Tập xem gần nhất của phim, null nếu chưa xem.
  Future<WatchHistoryEntry?> getHistoryEntry(String slug) async {
    try {
      return await _api.get(
        '/api/me/history/${_slug(slug)}',
        parse: (json) => WatchHistoryEntry.fromJson(json as Json),
      );
    } on AppException catch (e) {
      if (e.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<void> saveHistory(
    String slug, {
    required String serverName,
    required String episodeSlug,
    required String episodeName,
  }) => _api.put(
    '/api/me/history/${_slug(slug)}',
    data: {'serverName': serverName, 'episodeSlug': episodeSlug, 'episodeName': episodeName},
  );

  Future<void> removeHistory(String slug) => _api.delete('/api/me/history/${_slug(slug)}');

  Future<void> clearHistory() => _api.delete('/api/me/history');
}
