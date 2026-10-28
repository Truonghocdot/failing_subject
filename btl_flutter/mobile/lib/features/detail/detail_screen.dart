import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../core/api_client.dart';
import '../../core/widgets/app_network_image.dart';
import '../../core/widgets/error_view.dart';
import '../../core/widgets/expandable_text.dart';
import '../../core/widgets/loading_view.dart';
import '../../data/models/movie_detail.dart';
import '../../data/movie_query.dart';
import 'detail_providers.dart';
import 'widgets/detail_header.dart';
import '../library/library_providers.dart';
import '../library/widgets/favorite_button.dart';
import 'widgets/episode_section.dart';

class DetailScreen extends ConsumerWidget {
  const DetailScreen({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(movieDetailProvider(slug));

    return detail.when(
      loading: () => Scaffold(appBar: AppBar(), body: const LoadingView()),
      error: (error, _) => Scaffold(
        appBar: AppBar(),
        body: ErrorView(message: errorMessage(error), onRetry: () => ref.invalidate(movieDetailProvider(slug))),
      ),
      data: (movie) => _DetailView(movie: movie, onRefresh: () => ref.refresh(movieDetailProvider(slug).future)),
    );
  }
}

class _DetailView extends ConsumerStatefulWidget {
  const _DetailView({required this.movie, required this.onRefresh});

  final MovieDetail movie;
  final Future<void> Function() onRefresh;

  @override
  ConsumerState<_DetailView> createState() => _DetailViewState();
}

class _DetailViewState extends ConsumerState<_DetailView> {
  int? _serverIndex; // null = theo lịch sử xem (nếu có), ngược lại server đầu tiên

  MovieDetail get movie => widget.movie;

  void _openEpisode(Server server, Episode episode) {
    context.push(Routes.watch(movie.slug, server: movie.servers.indexOf(server), episode: episode.slug));
  }

  /// Tập xem dở (đăng nhập + đã xem phim này) → (server, tập) tương ứng trong dữ liệu hiện tại.
  (int, Episode)? _resumePoint() {
    final entry = ref.watch(historyEntryProvider(movie.slug)).value;
    if (entry == null) return null;
    final serverIndex = movie.servers.indexWhere((s) => s.name == entry.serverName);
    for (final (index, server) in movie.servers.indexed) {
      if (serverIndex >= 0 && index != serverIndex) continue;
      final episode = server.episodes.where((e) => e.slug == entry.episodeSlug).firstOrNull;
      if (episode != null) return (index, episode);
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final width = MediaQuery.sizeOf(context).width;
    final resume = _resumePoint();
    final serverIndex = (_serverIndex ?? resume?.$1 ?? 0).clamp(
      0,
      movie.servers.isEmpty ? 0 : movie.servers.length - 1,
    );
    final server = movie.servers.isEmpty ? null : movie.servers[serverIndex];

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: widget.onRefresh,
        edgeOffset: kToolbarHeight,
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              pinned: true,
              expandedHeight: width * 9 / 16,
              actions: [FavoriteButton(slug: movie.slug)],
              flexibleSpace: FlexibleSpaceBar(
                background: _Backdrop(url: movie.thumbUrl, decodeWidth: width),
              ),
            ),
            SliverToBoxAdapter(child: DetailHeader(movie: movie)),
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                child: _PlayButton(
                  movie: movie,
                  server: server,
                  resume: resume == null ? null : (movie.servers[resume.$1], resume.$2),
                  onPlay: _openEpisode,
                ),
              ),
            ),
            if (movie.showtimes != null)
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(16, 10, 16, 0),
                  child: Text(movie.showtimes!, style: TextStyle(color: Theme.of(context).colorScheme.tertiary)),
                ),
              ),
            if (movie.content.isNotEmpty)
              SliverToBoxAdapter(
                child: Padding(padding: const EdgeInsets.fromLTRB(16, 16, 16, 0), child: ExpandableText(movie.content)),
              ),
            SliverToBoxAdapter(child: _Credits(movie: movie)),
            if (movie.showsEpisodeGrid && server != null)
              SliverToBoxAdapter(
                child: EpisodeSection(
                  servers: movie.servers,
                  selectedServer: serverIndex,
                  watchedEpisodeSlug: resume != null && resume.$1 == serverIndex ? resume.$2.slug : null,
                  onServerChanged: (index) => setState(() => _serverIndex = index),
                  onEpisodeTap: (episode) => _openEpisode(server, episode),
                ),
              ),
            const SliverPadding(padding: EdgeInsets.only(bottom: 32)),
          ],
        ),
      ),
    );
  }
}

class _Backdrop extends StatelessWidget {
  const _Backdrop({required this.url, required this.decodeWidth});

  final String url;
  final double decodeWidth;

  @override
  Widget build(BuildContext context) {
    final background = Theme.of(context).scaffoldBackgroundColor;
    return Stack(
      fit: StackFit.expand,
      children: [
        AppNetworkImage(url: url, decodeWidth: decodeWidth),
        DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [Colors.black45, Colors.transparent, background],
              stops: const [0, 0.4, 1],
            ),
          ),
        ),
      ],
    );
  }
}

class _PlayButton extends StatelessWidget {
  const _PlayButton({required this.movie, required this.server, required this.resume, required this.onPlay});

  final MovieDetail movie;
  final Server? server;

  /// Tập xem dở – ưu tiên hơn tập đầu.
  final (Server, Episode)? resume;
  final void Function(Server server, Episode episode) onPlay;

  @override
  Widget build(BuildContext context) {
    final (Server, Episode)? target =
        resume ??
        switch (server?.episodes.firstOrNull) {
          final Episode first => (server!, first),
          null => null,
        };
    final label = switch (target) {
      null => 'Chưa có tập phim',
      (_, final episode) when resume != null => movie.showsEpisodeGrid ? 'Xem tiếp ${episode.label}' : 'Xem tiếp',
      (_, final episode) => movie.showsEpisodeGrid ? 'Xem ${episode.label}' : 'Xem phim',
    };

    return SizedBox(
      width: double.infinity,
      height: 48,
      child: FilledButton.icon(
        onPressed: target == null ? null : () => onPlay(target.$1, target.$2),
        icon: const Icon(Icons.play_arrow_rounded),
        label: Text(label),
      ),
    );
  }
}

class _Credits extends StatelessWidget {
  const _Credits({required this.movie});

  final MovieDetail movie;

  @override
  Widget build(BuildContext context) {
    final rows = <(String, String)>[
      if (movie.directors.isNotEmpty) ('Đạo diễn', movie.directors.join(', ')),
      if (movie.actors.isNotEmpty) ('Diễn viên', movie.actors.join(', ')),
    ];
    final theme = Theme.of(context);
    void open(MovieQuery query, String title) => context.push(Routes.list(query, title: title));

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (movie.genres.isNotEmpty || movie.countries.isNotEmpty)
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final genre in movie.genres)
                  ActionChip(label: Text(genre.name), onPressed: () => open(MovieQuery.genre(genre.slug), genre.name)),
                for (final country in movie.countries)
                  ActionChip(
                    avatar: const Icon(Icons.public, size: 18),
                    label: Text(country.name),
                    onPressed: () => open(MovieQuery.country(country.slug), country.name),
                  ),
              ],
            ),
          for (final (label, value) in rows)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text.rich(
                TextSpan(
                  children: [
                    TextSpan(
                      text: '$label: ',
                      style: TextStyle(color: theme.colorScheme.onSurfaceVariant),
                    ),
                    TextSpan(text: value),
                  ],
                ),
                maxLines: 3,
                overflow: TextOverflow.ellipsis,
              ),
            ),
        ],
      ),
    );
  }
}
