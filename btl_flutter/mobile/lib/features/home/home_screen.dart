import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../core/api_client.dart';
import '../../core/widgets/error_view.dart';
import '../../core/widgets/loading_view.dart';
import '../../core/widgets/section_row.dart';
import '../../data/movie_query.dart';
import '../library/library_providers.dart';
import 'home_providers.dart';
import 'widgets/home_banner.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  static const _bannerCount = 5;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);
    final continueWatching = ref.watch(continueWatchingProvider).value ?? const [];

    return Scaffold(
      appBar: AppBar(title: const Text('VsMovie')),
      body: home.when(
        loading: () => const LoadingView(),
        error: (error, _) => ErrorView(message: errorMessage(error), onRetry: () => ref.invalidate(homeProvider)),
        data: (sections) {
          if (sections.isEmpty) {
            return const ErrorView(message: 'Chưa có phim nào để hiển thị');
          }
          final banner = sections.first.items.take(_bannerCount).toList();
          return RefreshIndicator(
            onRefresh: () => ref.refresh(homeProvider.future),
            child: ListView(
              padding: const EdgeInsets.only(bottom: 24),
              children: [
                if (banner.isNotEmpty) HomeBanner(movies: banner),
                if (continueWatching.isNotEmpty)
                  SectionRow(
                    title: 'Xem tiếp',
                    movies: [for (final entry in continueWatching) entry.movie],
                    subtitles: [for (final entry in continueWatching) entry.episodeLabel],
                    onSeeAll: () => context.go(Routes.library),
                  ),
                for (final section in sections)
                  SectionRow(
                    title: section.title,
                    movies: section.items,
                    onSeeAll: () => context.push(Routes.list(MovieQuery.list(section.key), title: section.title)),
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
