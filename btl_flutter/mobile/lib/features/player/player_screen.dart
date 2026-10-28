import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/widgets/error_view.dart';
import '../../core/widgets/loading_view.dart';
import '../../data/library_repository.dart';
import '../../data/models/movie_detail.dart';
import '../auth/auth_providers.dart';
import '../detail/detail_providers.dart';
import '../library/library_providers.dart';
import 'embed_player.dart';
import 'episode_picker_sheet.dart';

/// Màn hình xem phim: xoay ngang, ẩn thanh hệ thống, thanh điều khiển tự ẩn sau vài giây.
/// Dữ liệu tập lấy từ [movieDetailProvider] (đã có sẵn khi đi từ trang chi tiết).
class PlayerScreen extends ConsumerStatefulWidget {
  const PlayerScreen({super.key, required this.slug, required this.serverIndex, required this.episodeSlug});

  final String slug;
  final int serverIndex;
  final String episodeSlug;

  @override
  ConsumerState<PlayerScreen> createState() => _PlayerScreenState();
}

class _PlayerScreenState extends ConsumerState<PlayerScreen> {
  static const _autoHideDelay = Duration(seconds: 4);

  late int _serverIndex = widget.serverIndex;
  late String _episodeSlug = widget.episodeSlug;
  bool _controlsVisible = true;
  Timer? _hideTimer;

  /// "server/tập" đã ghi lịch sử gần nhất – tránh ghi lặp mỗi lần build.
  String? _recordedKey;

  @override
  void initState() {
    super.initState();
    SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    _scheduleHide();
  }

  @override
  void dispose() {
    _hideTimer?.cancel();
    SystemChrome.setPreferredOrientations(const []); // về mặc định của app
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  void _scheduleHide() {
    _hideTimer?.cancel();
    _hideTimer = Timer(_autoHideDelay, () {
      if (mounted) setState(() => _controlsVisible = false);
    });
  }

  /// Đã đăng nhập → ghi tập đang xem lên server (lỗi thì bỏ qua, không làm gián đoạn việc xem).
  Future<void> _recordHistory(Server server, Episode episode) async {
    if (ref.read(currentUserIdProvider) == null) return;
    try {
      await ref
          .read(libraryRepositoryProvider)
          .saveHistory(widget.slug, serverName: server.name, episodeSlug: episode.slug, episodeName: episode.name);
      if (mounted) invalidateHistory(ref, widget.slug);
    } catch (e) {
      debugPrint('Không ghi được lịch sử xem: $e');
    }
  }

  void _showControls() {
    setState(() => _controlsVisible = true);
    _scheduleHide();
  }

  void _select(int serverIndex, Episode episode) {
    setState(() {
      _serverIndex = serverIndex;
      _episodeSlug = episode.slug;
    });
    _scheduleHide();
  }

  Future<void> _pickEpisode(MovieDetail movie) async {
    _hideTimer?.cancel();
    final picked = await showEpisodePickerSheet(
      context,
      servers: movie.servers,
      serverIndex: _serverIndex,
      episodeSlug: _episodeSlug,
    );
    if (!mounted) return;
    if (picked != null) {
      _select(picked.serverIndex, picked.episode);
    } else {
      _scheduleHide();
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = ref.watch(movieDetailProvider(widget.slug));

    return Scaffold(
      backgroundColor: Colors.black,
      body: detail.when(
        loading: () => const LoadingView(),
        error: (error, _) => _Fallback(
          child: ErrorView(
            message: errorMessage(error),
            onRetry: () => ref.invalidate(movieDetailProvider(widget.slug)),
          ),
        ),
        data: _buildPlayer,
      ),
    );
  }

  Widget _buildPlayer(MovieDetail movie) {
    final serverIndex = _serverIndex < movie.servers.length ? _serverIndex : 0;
    final server = movie.servers.elementAtOrNull(serverIndex);
    final episodes = server?.episodes ?? const <Episode>[];
    final index = episodes.indexWhere((e) => e.slug == _episodeSlug);
    if (server == null || index < 0) {
      return const _Fallback(child: ErrorView(message: 'Không tìm thấy tập phim'));
    }

    final episode = episodes[index];
    final recordKey = '$serverIndex/${episode.slug}';
    if (recordKey != _recordedKey) {
      _recordedKey = recordKey;
      WidgetsBinding.instance.addPostFrameCallback((_) => _recordHistory(server, episode));
    }
    final previous = index > 0 ? episodes[index - 1] : null;
    final next = index < episodes.length - 1 ? episodes[index + 1] : null;

    return Stack(
      fit: StackFit.expand,
      children: [
        ref.watch(embedPlayerBuilderProvider)(context, episode.embedUrl),
        Positioned(
          top: 0,
          left: 0,
          right: 0,
          child: IgnorePointer(
            ignoring: !_controlsVisible,
            child: AnimatedOpacity(
              opacity: _controlsVisible ? 1 : 0,
              duration: const Duration(milliseconds: 250),
              child: _ControlBar(
                title: movie.name,
                subtitle: movie.showsEpisodeGrid ? '${episode.label} • ${server.name}' : server.name,
                onPrevious: previous == null ? null : () => _select(serverIndex, previous),
                onNext: next == null ? null : () => _select(serverIndex, next),
                onPickEpisode: movie.hasEpisodes ? () => _pickEpisode(movie) : null,
                onHide: () => setState(() => _controlsVisible = false),
              ),
            ),
          ),
        ),
        if (!_controlsVisible)
          Positioned(
            top: 4,
            right: 8,
            child: IconButton.filledTonal(
              tooltip: 'Hiện điều khiển',
              onPressed: _showControls,
              icon: const Icon(Icons.expand_more),
            ),
          ),
      ],
    );
  }
}

class _ControlBar extends StatelessWidget {
  const _ControlBar({
    required this.title,
    required this.subtitle,
    required this.onPrevious,
    required this.onNext,
    required this.onPickEpisode,
    required this.onHide,
  });

  final String title;
  final String subtitle;
  final VoidCallback? onPrevious;
  final VoidCallback? onNext;
  final VoidCallback? onPickEpisode;
  final VoidCallback onHide;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return DecoratedBox(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Colors.black87, Colors.transparent],
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(4, 4, 4, 20),
          child: IconButtonTheme(
            data: IconButtonThemeData(style: IconButton.styleFrom(foregroundColor: Colors.white)),
            child: Row(
              children: [
                IconButton(
                  tooltip: 'Quay lại',
                  onPressed: () => Navigator.of(context).maybePop(),
                  icon: const Icon(Icons.arrow_back),
                ),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: textTheme.titleMedium?.copyWith(color: Colors.white),
                      ),
                      Text(subtitle, maxLines: 1, style: textTheme.bodySmall?.copyWith(color: Colors.white70)),
                    ],
                  ),
                ),
                IconButton(tooltip: 'Tập trước', onPressed: onPrevious, icon: const Icon(Icons.skip_previous)),
                IconButton(tooltip: 'Tập sau', onPressed: onNext, icon: const Icon(Icons.skip_next)),
                IconButton(tooltip: 'Chọn tập', onPressed: onPickEpisode, icon: const Icon(Icons.playlist_play)),
                IconButton(tooltip: 'Ẩn điều khiển', onPressed: onHide, icon: const Icon(Icons.expand_less)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Lỗi trên nền đen + nút quay lại (màn player không có AppBar).
class _Fallback extends StatelessWidget {
  const _Fallback({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Stack(
        children: [
          child,
          IconButton(
            tooltip: 'Quay lại',
            color: Colors.white,
            onPressed: () => Navigator.of(context).maybePop(),
            icon: const Icon(Icons.arrow_back),
          ),
        ],
      ),
    );
  }
}
