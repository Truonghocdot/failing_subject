import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/paged.dart';
import '../../data/library_repository.dart';
import '../../data/models/movie_summary.dart';
import '../../data/models/paged_result.dart';
import '../../data/movie_query.dart';
import '../../data/movie_repository.dart';
import '../auth/auth_providers.dart';

typedef PagedMovies = Paged<MovieSummary>;

/// Lưới phim cho một [MovieQuery]: mỗi query một instance, tự huỷ khi rời màn hình.
class PagedMoviesNotifier extends PagedNotifier<MovieSummary> {
  PagedMoviesNotifier(this.query);

  final MovieQuery query;

  @override
  Future<PagedMovies> build() {
    ref.watch(movieRepositoryProvider);
    // Yêu thích là dữ liệu cá nhân → tải lại khi đăng nhập/đăng xuất
    if (query.kind == MovieQueryKind.favorites) ref.watch(currentUserIdProvider);
    return super.build();
  }

  @override
  Future<PagedResult<MovieSummary>> fetchPage(int page) {
    final movies = ref.read(movieRepositoryProvider);
    return switch (query.kind) {
      MovieQueryKind.list => movies.getList(query.value, page: page),
      MovieQueryKind.genre => movies.getByGenre(query.value, page: page),
      MovieQueryKind.country => movies.getByCountry(query.value, page: page),
      MovieQueryKind.year => movies.getByYear(int.parse(query.value), page: page),
      MovieQueryKind.search => movies.search(query.value, page: page),
      MovieQueryKind.favorites => ref.read(libraryRepositoryProvider).getFavorites(page: page),
    };
  }

  @override
  Object keyOf(MovieSummary item) => item.slug;
}

final pagedMoviesProvider = AsyncNotifierProvider.autoDispose.family<PagedMoviesNotifier, PagedMovies, MovieQuery>(
  PagedMoviesNotifier.new,
);
