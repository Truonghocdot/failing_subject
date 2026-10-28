import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api_client.dart';
import '../../../core/widgets/empty_view.dart';
import '../../../core/widgets/error_view.dart';
import '../../../core/widgets/loading_view.dart';
import '../../../core/widgets/movie_card.dart';
import '../../../data/models/movie_summary.dart';
import '../../../data/movie_query.dart';
import '../paged_movies_provider.dart';

/// Lưới phim cuộn vô hạn cho một [MovieQuery]: tự tải trang kế khi gần cuối, kéo xuống để làm mới.
class MovieGrid extends ConsumerWidget {
  const MovieGrid({super.key, required this.query, this.emptyMessage = 'Không có phim nào', this.onMovieOpened});

  static const _padding = 16.0;
  static const _spacing = 12.0;
  static const _minCardWidth = 100.0;
  static const _loadMoreThreshold = 400.0;

  final MovieQuery query;
  final String emptyMessage;

  /// Gọi trước khi mở trang chi tiết (VD lưu từ khoá tìm kiếm).
  final ValueChanged<MovieSummary>? onMovieOpened;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final provider = pagedMoviesProvider(query);
    final paged = ref.watch(provider);

    return paged.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(message: errorMessage(error), onRetry: () => ref.invalidate(provider)),
      data: (data) {
        if (data.items.isEmpty) return EmptyView(icon: Icons.search_off, message: emptyMessage);

        return RefreshIndicator(
          onRefresh: () => ref.refresh(provider.future),
          child: NotificationListener<ScrollNotification>(
            onNotification: (notification) {
              if (notification.metrics.extentAfter < _loadMoreThreshold) ref.read(provider.notifier).loadMore();
              return false;
            },
            child: LayoutBuilder(
              builder: (context, constraints) {
                final available = constraints.maxWidth - _padding * 2;
                final columns = math.max(3, ((available + _spacing) / (_minCardWidth + 32 + _spacing)).floor());
                final cardWidth = (available - _spacing * (columns - 1)) / columns;

                return CustomScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  slivers: [
                    SliverPadding(
                      padding: const EdgeInsets.fromLTRB(_padding, 12, _padding, 0),
                      sliver: SliverGrid.builder(
                        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
                          crossAxisCount: columns,
                          crossAxisSpacing: _spacing,
                          mainAxisSpacing: 16,
                          mainAxisExtent: cardWidth * 1.5 + 64,
                        ),
                        itemCount: data.items.length,
                        itemBuilder: (context, index) {
                          final movie = data.items[index];
                          return MovieCard(
                            movie: movie,
                            width: cardWidth,
                            onOpened: onMovieOpened == null ? null : () => onMovieOpened!(movie),
                          );
                        },
                      ),
                    ),
                    SliverToBoxAdapter(
                      child: _Footer(data: data, onRetry: () => ref.read(provider.notifier).loadMore()),
                    ),
                  ],
                );
              },
            ),
          ),
        );
      },
    );
  }
}

class _Footer extends StatelessWidget {
  const _Footer({required this.data, required this.onRetry});

  final PagedMovies data;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final muted = TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant);
    final Widget child;
    if (data.isLoadingMore) {
      child = const SizedBox.square(dimension: 28, child: CircularProgressIndicator(strokeWidth: 3));
    } else if (data.loadMoreError != null) {
      child = Column(
        children: [
          Text(data.loadMoreError!, style: muted, textAlign: TextAlign.center),
          TextButton.icon(onPressed: onRetry, icon: const Icon(Icons.refresh), label: const Text('Tải tiếp')),
        ],
      );
    } else if (!data.hasMore) {
      child = Text('Đã hiển thị ${data.items.length} phim', style: muted);
    } else {
      child = const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 32),
      child: Center(child: child),
    );
  }
}
