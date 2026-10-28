import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../core/api_client.dart';
import '../../core/widgets/error_view.dart';
import '../../core/widgets/loading_view.dart';
import '../../data/models/app_user.dart';
import '../../data/movie_query.dart';
import '../auth/auth_providers.dart';
import '../browse/widgets/movie_grid.dart';
import 'widgets/history_list.dart';

/// Tab Thư viện: khách → mời đăng nhập; đã đăng nhập → Yêu thích | Lịch sử.
class LibraryScreen extends ConsumerWidget {
  const LibraryScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ref
        .watch(authProvider)
        .when(
          loading: () => Scaffold(
            appBar: AppBar(title: const Text('Thư viện')),
            body: const LoadingView(),
          ),
          error: (error, _) => Scaffold(
            appBar: AppBar(title: const Text('Thư viện')),
            body: ErrorView(message: errorMessage(error), onRetry: () => ref.invalidate(authProvider)),
          ),
          data: (user) => user == null ? const _GuestView() : _SignedInView(user: user),
        );
  }
}

class _GuestView extends StatelessWidget {
  const _GuestView();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Thư viện')),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(32),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.video_library_outlined, size: 64, color: theme.colorScheme.primary),
              const SizedBox(height: 16),
              Text('Lưu phim yêu thích & xem tiếp', style: theme.textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(
                'Đăng nhập để đồng bộ danh sách yêu thích và lịch sử xem trên mọi thiết bị.',
                textAlign: TextAlign.center,
                style: TextStyle(color: theme.colorScheme.onSurfaceVariant),
              ),
              const SizedBox(height: 24),
              FilledButton(onPressed: () => context.push(Routes.login), child: const Text('Đăng nhập')),
              const SizedBox(height: 8),
              OutlinedButton(onPressed: () => context.push(Routes.register), child: const Text('Tạo tài khoản')),
            ],
          ),
        ),
      ),
    );
  }
}

class _SignedInView extends ConsumerWidget {
  const _SignedInView({required this.user});

  final AppUser user;

  Future<void> _logout(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Đăng xuất?'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Huỷ')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đăng xuất')),
        ],
      ),
    );
    if (confirmed == true) await ref.read(authProvider.notifier).logout();
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Row(
            children: [
              CircleAvatar(radius: 18, child: Text(user.initial)),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(user.displayName, style: theme.textTheme.titleMedium, overflow: TextOverflow.ellipsis),
                    Text(
                      user.email,
                      style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
            ],
          ),
          actions: [
            IconButton(tooltip: 'Đăng xuất', onPressed: () => _logout(context, ref), icon: const Icon(Icons.logout)),
          ],
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Yêu thích'),
              Tab(text: 'Lịch sử'),
            ],
          ),
        ),
        body: const TabBarView(
          children: [
            MovieGrid(
              query: MovieQuery.favorites,
              emptyMessage: 'Chưa có phim yêu thích.\nBấm ♥ ở trang chi tiết phim để lưu.',
            ),
            HistoryList(),
          ],
        ),
      ),
    );
  }
}
