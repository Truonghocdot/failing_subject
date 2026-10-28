import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../core/api_client.dart';
import '../../../core/widgets/app_network_image.dart';
import '../../../core/widgets/empty_view.dart';
import '../../../core/widgets/error_view.dart';
import '../../../core/widgets/loading_view.dart';
import '../../../data/models/watch_history_entry.dart';
import '../library_providers.dart';

/// Tab Lịch sử: mới nhất trước, vuốt để xoá, cuộn để tải thêm.
class HistoryList extends ConsumerWidget {
  const HistoryList({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final history = ref.watch(historyProvider);
    final notifier = ref.read(historyProvider.notifier);

    return history.when(
      loading: () => const LoadingView(),
      error: (error, _) => ErrorView(message: errorMessage(error), onRetry: () => ref.invalidate(historyProvider)),
      data: (data) {
        if (data.items.isEmpty) {
          return const EmptyView(icon: Icons.history, message: 'Chưa xem phim nào.\nPhim bạn xem sẽ hiện ở đây.');
        }
        return RefreshIndicator(
          onRefresh: () => ref.refresh(historyProvider.future),
          child: NotificationListener<ScrollNotification>(
            onNotification: (n) {
              if (n.metrics.extentAfter < 400) notifier.loadMore();
              return false;
            },
            child: ListView.builder(
              physics: const AlwaysScrollableScrollPhysics(),
              itemCount: data.items.length + 1,
              itemBuilder: (context, index) {
                if (index == 0) {
                  return Align(
                    alignment: Alignment.centerRight,
                    child: TextButton.icon(
                      onPressed: () => _confirmClear(context, notifier),
                      icon: const Icon(Icons.delete_sweep_outlined),
                      label: const Text('Xoá tất cả'),
                    ),
                  );
                }
                final entry = data.items[index - 1];
                return Dismissible(
                  key: ValueKey(entry.movie.slug),
                  direction: DismissDirection.endToStart,
                  background: Container(
                    alignment: Alignment.centerRight,
                    padding: const EdgeInsets.only(right: 24),
                    color: Theme.of(context).colorScheme.errorContainer,
                    child: const Icon(Icons.delete_outline),
                  ),
                  onDismissed: (_) => notifier.remove(entry.movie.slug).catchError((Object e) {
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(errorMessage(e))));
                    }
                  }),
                  child: _HistoryTile(entry: entry),
                );
              },
            ),
          ),
        );
      },
    );
  }

  Future<void> _confirmClear(BuildContext context, HistoryNotifier notifier) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Xoá toàn bộ lịch sử?'),
        content: const Text('Bạn sẽ không thể "Xem tiếp" các phim đã xem.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Huỷ')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Xoá')),
        ],
      ),
    );
    if (confirmed == true) await notifier.clear();
  }
}

class _HistoryTile extends StatelessWidget {
  const _HistoryTile({required this.entry});

  final WatchHistoryEntry entry;

  @override
  Widget build(BuildContext context) {
    final muted = TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant);
    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      leading: ClipRRect(
        borderRadius: BorderRadius.circular(6),
        child: SizedBox(width: 48, height: 72, child: AppNetworkImage(url: entry.movie.posterUrl, decodeWidth: 48)),
      ),
      title: Text(entry.movie.name, maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${entry.episodeLabel} • ${entry.serverName}\n${timeAgo(entry.watchedAt)}', style: muted),
      isThreeLine: true,
      onTap: () => context.push(Routes.movie(entry.movie.slug)),
    );
  }
}

/// "Vừa xong", "5 phút trước", "2 giờ trước", "3 ngày trước", hoặc ngày dd/MM/yyyy.
String timeAgo(DateTime time, {DateTime? now}) {
  final diff = (now ?? DateTime.now()).difference(time);
  if (diff.inMinutes < 1) return 'Vừa xong';
  if (diff.inHours < 1) return '${diff.inMinutes} phút trước';
  if (diff.inDays < 1) return '${diff.inHours} giờ trước';
  if (diff.inDays < 7) return '${diff.inDays} ngày trước';
  String two(int v) => v.toString().padLeft(2, '0');
  return '${two(time.day)}/${two(time.month)}/${time.year}';
}
