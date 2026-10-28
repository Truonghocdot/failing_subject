import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/paged.dart';
import '../../data/library_repository.dart';
import '../../data/models/paged_result.dart';
import '../../data/models/watch_history_entry.dart';
import '../../data/movie_query.dart';
import '../auth/auth_providers.dart';
import '../browse/paged_movies_provider.dart';

/// Trạng thái ♥ của một phim; [toggle] cập nhật lạc quan, lỗi thì trả lại trạng thái cũ.
class FavoriteStatusNotifier extends AsyncNotifier<bool> {
  FavoriteStatusNotifier(this.slug);

  final String slug;

  @override
  Future<bool> build() async {
    if (ref.watch(currentUserIdProvider) == null) return false;
    return ref.read(libraryRepositoryProvider).isFavorite(slug);
  }

  Future<void> toggle() async {
    final previous = state.value ?? false;
    state = AsyncData(!previous);
    try {
      final library = ref.read(libraryRepositoryProvider);
      await (previous ? library.removeFavorite(slug) : library.addFavorite(slug));
      ref.invalidate(pagedMoviesProvider(MovieQuery.favorites));
    } catch (_) {
      if (ref.mounted) state = AsyncData(previous);
      rethrow;
    }
  }
}

final favoriteStatusProvider = AsyncNotifierProvider.autoDispose.family<FavoriteStatusNotifier, bool, String>(
  FavoriteStatusNotifier.new,
);

/// Tập xem gần nhất của một phim (nút "Xem tiếp" ở trang chi tiết).
final historyEntryProvider = FutureProvider.autoDispose.family<WatchHistoryEntry?, String>((ref, slug) {
  if (ref.watch(currentUserIdProvider) == null) return null;
  return ref.read(libraryRepositoryProvider).getHistoryEntry(slug);
});

/// Hàng "Xem tiếp" ở trang chủ.
final continueWatchingProvider = FutureProvider.autoDispose<List<WatchHistoryEntry>>((ref) async {
  if (ref.watch(currentUserIdProvider) == null) return const [];
  return (await ref.read(libraryRepositoryProvider).getHistory()).items.take(12).toList();
});

/// Toàn bộ lịch sử xem (tab Lịch sử).
class HistoryNotifier extends PagedNotifier<WatchHistoryEntry> {
  @override
  Future<Paged<WatchHistoryEntry>> build() {
    ref.watch(currentUserIdProvider);
    return super.build();
  }

  @override
  Future<PagedResult<WatchHistoryEntry>> fetchPage(int page) =>
      ref.read(libraryRepositoryProvider).getHistory(page: page);

  @override
  Object keyOf(WatchHistoryEntry item) => item.movie.slug;

  Future<void> remove(String slug) async {
    final current = state.value;
    if (current != null) {
      state = AsyncData(
        current.copyWith(
          items: current.items.where((e) => e.movie.slug != slug).toList(),
          totalItems: current.totalItems - 1,
        ),
      );
    }
    try {
      await ref.read(libraryRepositoryProvider).removeHistory(slug);
    } finally {
      _historyChanged(slug);
    }
  }

  Future<void> clear() async {
    await ref.read(libraryRepositoryProvider).clearHistory();
    ref.invalidateSelf();
    ref.invalidate(continueWatchingProvider);
    ref.invalidate(historyEntryProvider);
  }

  void _historyChanged(String slug) {
    ref.invalidate(continueWatchingProvider);
    ref.invalidate(historyEntryProvider(slug));
  }
}

final historyProvider = AsyncNotifierProvider.autoDispose<HistoryNotifier, Paged<WatchHistoryEntry>>(
  HistoryNotifier.new,
);

/// Gọi sau khi ghi lịch sử (Player) để mọi nơi hiển thị lịch sử cập nhật.
void invalidateHistory(WidgetRef ref, String slug) {
  ref.invalidate(historyEntryProvider(slug));
  ref.invalidate(continueWatchingProvider);
  ref.invalidate(historyProvider);
}
