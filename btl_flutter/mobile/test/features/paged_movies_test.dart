import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api_client.dart';
import 'package:mobile/data/models/movie_summary.dart';
import 'package:mobile/data/models/paged_result.dart';
import 'package:mobile/data/movie_query.dart';
import 'package:mobile/data/movie_repository.dart';
import 'package:mobile/features/browse/paged_movies_provider.dart';

import '../support/fakes.dart';

void main() {
  const query = MovieQuery.list('phim-bo');

  (ProviderContainer, FakeMovieRepository) setup({int totalPages = 2}) {
    final repo = FakeMovieRepository()..totalPages = totalPages;
    final container = ProviderContainer(
      retry: (_, _) => null,
      overrides: [movieRepositoryProvider.overrideWithValue(repo)],
    );
    addTearDown(container.dispose);
    container.listen(pagedMoviesProvider(query), (_, _) {}); // giữ provider autoDispose sống
    return (container, repo);
  }

  test('Tải trang 1 rồi nối trang 2, hết trang thì dừng', () async {
    final (container, repo) = setup();
    final notifier = container.read(pagedMoviesProvider(query).notifier);

    final first = await container.read(pagedMoviesProvider(query).future);
    expect(first.items.length, 3);
    expect(first.hasMore, isTrue);

    await notifier.loadMore();
    final second = container.read(pagedMoviesProvider(query)).requireValue;
    expect(second.items.length, 6);
    expect(second.page, 2);
    expect(second.hasMore, isFalse);

    await notifier.loadMore(); // không gọi thêm
    expect(repo.pagedCalls, ['list-phim-bo:1', 'list-phim-bo:2']);
  });

  test('Lỗi khi tải thêm → giữ dữ liệu cũ, có thông báo, tải lại được', () async {
    final (container, repo) = setup();
    final notifier = container.read(pagedMoviesProvider(query).notifier);
    await container.read(pagedMoviesProvider(query).future);

    repo.error = const AppException('Mất kết nối');
    await notifier.loadMore();
    var state = container.read(pagedMoviesProvider(query)).requireValue;
    expect(state.items.length, 3);
    expect(state.loadMoreError, 'Mất kết nối');
    expect(state.isLoadingMore, isFalse);

    repo.error = null;
    await notifier.loadMore();
    state = container.read(pagedMoviesProvider(query)).requireValue;
    expect(state.items.length, 6);
    expect(state.loadMoreError, isNull);
  });

  test('PagedMovies.append bỏ phim trùng, trang rỗng thì coi như hết', () {
    const a = MovieSummary(slug: 'a', name: 'A', posterUrl: '', thumbUrl: '');
    const b = MovieSummary(slug: 'b', name: 'B', posterUrl: '', thumbUrl: '');
    const current = PagedMovies(items: [a], page: 1, totalPages: 5, totalItems: 10);

    final merged = current.append(
      const PagedResult(items: [a, b], page: 2, pageSize: 2, totalItems: 10, totalPages: 5),
      (m) => m.slug,
    );
    expect(merged.items.map((m) => m.slug), ['a', 'b']);
    expect(merged.hasMore, isTrue);

    final ended = merged.append(
      const PagedResult(items: [], page: 3, pageSize: 2, totalItems: 10, totalPages: 5),
      (m) => m.slug,
    );
    expect(ended.hasMore, isFalse);
  });

  test('MovieQuery.tryParse + tiêu đề mặc định', () {
    expect(MovieQuery.tryParse('genre', 'lang-man'), const MovieQuery.genre('lang-man'));
    expect(MovieQuery.tryParse('year', 'abc'), isNull);
    expect(MovieQuery.tryParse('unknown', 'x'), isNull);
    expect(MovieQuery.year(2025).defaultTitle, 'Phim năm 2025');
    expect(const MovieQuery.list('phim-le').defaultTitle, 'Phim lẻ');
  });
}
