import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import 'models/home_section.dart';
import 'models/json_helpers.dart';
import 'models/movie_detail.dart';
import 'models/movie_summary.dart';
import 'models/paged_result.dart';
import 'models/taxonomy.dart';

final movieRepositoryProvider = Provider<MovieRepository>((ref) => MovieRepository(ref.watch(apiClientProvider)));

/// Gọi các API phim của backend (docs/03-backend-dotnet.md §3).
class MovieRepository {
  MovieRepository(this._api);

  final ApiClient _api;

  Future<List<HomeSection>> getHome() =>
      _api.get('/api/home', parse: (json) => parseList((json as Json)['sections'], HomeSection.fromJson));

  /// [list]: phim-moi-cap-nhat | phim-bo | phim-le | phim-chieu-rap
  Future<PagedResult<MovieSummary>> getList(String list, {int page = 1}) =>
      _paged('/api/movies', query: {'list': list, 'page': page});

  Future<PagedResult<MovieSummary>> search(String keyword, {int page = 1}) =>
      _paged('/api/movies/search', query: {'q': keyword, 'page': page});

  Future<PagedResult<MovieSummary>> getByGenre(String slug, {int page = 1}) =>
      _paged('/api/genres/${Uri.encodeComponent(slug)}/movies', query: {'page': page});

  Future<PagedResult<MovieSummary>> getByCountry(String slug, {int page = 1}) =>
      _paged('/api/countries/${Uri.encodeComponent(slug)}/movies', query: {'page': page});

  Future<PagedResult<MovieSummary>> getByYear(int year, {int page = 1}) =>
      _paged('/api/years/$year/movies', query: {'page': page});

  Future<MovieDetail> getDetail(String slug) =>
      _api.get('/api/movies/${Uri.encodeComponent(slug)}', parse: (json) => MovieDetail.fromJson(json as Json));

  Future<List<Taxonomy>> getGenres() => _api.get('/api/genres', parse: (json) => parseList(json, Taxonomy.fromJson));

  Future<List<Taxonomy>> getCountries() =>
      _api.get('/api/countries', parse: (json) => parseList(json, Taxonomy.fromJson));

  Future<PagedResult<MovieSummary>> _paged(String path, {required Map<String, Object?> query}) =>
      _api.get(path, query: query, parse: (json) => PagedResult.fromJson(json as Json, MovieSummary.fromJson));
}
