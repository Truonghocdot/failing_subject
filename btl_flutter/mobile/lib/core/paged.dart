import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../data/models/paged_result.dart';
import 'api_client.dart';

/// Danh sách đã tải qua nhiều trang.
class Paged<T> {
  const Paged({
    required this.items,
    required this.page,
    required this.totalPages,
    required this.totalItems,
    this.isLoadingMore = false,
    this.loadMoreError,
  });

  factory Paged.first(PagedResult<T> result) =>
      Paged(items: result.items, page: result.page, totalPages: result.totalPages, totalItems: result.totalItems);

  final List<T> items;
  final int page;
  final int totalPages;
  final int totalItems;
  final bool isLoadingMore;
  final String? loadMoreError;

  bool get hasMore => page < totalPages;

  Paged<T> copyWith({List<T>? items, int? totalItems, bool? isLoadingMore, String? loadMoreError}) => Paged(
    items: items ?? this.items,
    page: page,
    totalPages: totalPages,
    totalItems: totalItems ?? this.totalItems,
    isLoadingMore: isLoadingMore ?? this.isLoadingMore,
    loadMoreError: loadMoreError,
  );

  /// Nối trang mới, bỏ phần tử trùng theo [keyOf] (danh sách nguồn có thể dịch chuyển khi có phim mới).
  Paged<T> append(PagedResult<T> next, Object Function(T item) keyOf) {
    final seen = items.map(keyOf).toSet();
    return Paged(
      items: [...items, ...next.items.where((item) => seen.add(keyOf(item)))],
      page: next.page,
      // trang rỗng → coi như hết để không gọi mãi
      totalPages: next.items.isEmpty ? next.page : next.totalPages,
      totalItems: next.totalItems,
    );
  }
}

/// Khung chung: [build] tải trang 1, [loadMore] nối trang kế. Lớp con chỉ cần [fetchPage] + [keyOf].
/// Lớp con override [build] để `ref.watch` phụ thuộc rồi gọi `super.build()`.
abstract class PagedNotifier<T> extends AsyncNotifier<Paged<T>> {
  Future<PagedResult<T>> fetchPage(int page);

  Object keyOf(T item);

  @override
  Future<Paged<T>> build() async => Paged.first(await fetchPage(1));

  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || state.isLoading || !current.hasMore || current.isLoadingMore) return;

    state = AsyncData(current.copyWith(isLoadingMore: true));
    try {
      final next = await fetchPage(current.page + 1);
      if (ref.mounted) state = AsyncData(current.append(next, keyOf));
    } catch (error) {
      if (ref.mounted) state = AsyncData(current.copyWith(isLoadingMore: false, loadMoreError: errorMessage(error)));
    }
  }
}
