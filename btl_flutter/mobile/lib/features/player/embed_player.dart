import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:webview_flutter/webview_flutter.dart';
import 'package:webview_flutter_android/webview_flutter_android.dart';

import '../../app/env.dart';
import '../../core/widgets/error_view.dart';

typedef EmbedPlayerBuilder = Widget Function(BuildContext context, String url);

/// Cho phép test thay WebView (cần platform thật) bằng widget giả.
final embedPlayerBuilderProvider = Provider<EmbedPlayerBuilder>(
  (ref) =>
      (context, url) => EmbedPlayer(url: url),
);

/// Chỉ cho khung chính điều hướng trong domain trình phát; chặn quảng cáo/popup chuyển trang ra ngoài.
/// Khung con (iframe trong player) không bị chặn.
bool isAllowedPlayerNavigation(
  String url, {
  required bool isMainFrame,
  String hostSuffix = Env.playerAllowedHostSuffix,
}) {
  if (!isMainFrame) return true;
  if (url == 'about:blank') return true;
  final uri = Uri.tryParse(url);
  if (uri == null || (uri.scheme != 'https' && uri.scheme != 'http')) return false;
  return uri.host == hostSuffix || uri.host.endsWith('.$hostSuffix');
}

/// Trình phát nhúng của nguồn (JW Player trong trang HTML) chạy trong WebView.
class EmbedPlayer extends StatefulWidget {
  const EmbedPlayer({super.key, required this.url});

  final String url;

  @override
  State<EmbedPlayer> createState() => _EmbedPlayerState();
}

class _EmbedPlayerState extends State<EmbedPlayer> {
  late final WebViewController _controller;
  bool _loading = true;
  String? _error;

  /// Video toàn màn hình do trang yêu cầu (nút fullscreen của JW Player) – chỉ Android.
  Widget? _fullscreenWidget;
  VoidCallback? _exitFullscreen;

  @override
  void initState() {
    super.initState();
    _controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setBackgroundColor(Colors.black)
      ..setNavigationDelegate(
        NavigationDelegate(
          onNavigationRequest: (request) => isAllowedPlayerNavigation(request.url, isMainFrame: request.isMainFrame)
              ? NavigationDecision.navigate
              : NavigationDecision.prevent,
          onPageStarted: (_) => _update(loading: true),
          onPageFinished: (_) => _update(loading: false),
          onWebResourceError: (error) {
            if (error.isForMainFrame ?? false) {
              _update(loading: false, error: 'Không tải được trình phát (${error.description})');
            }
          },
        ),
      );

    final platform = _controller.platform;
    if (platform is AndroidWebViewController) {
      platform
        ..setMediaPlaybackRequiresUserGesture(false)
        ..setCustomWidgetCallbacks(
          onShowCustomWidget: (widget, onHidden) => setState(() {
            _fullscreenWidget = widget;
            _exitFullscreen = onHidden;
          }),
          onHideCustomWidget: () => setState(() {
            _fullscreenWidget = null;
            _exitFullscreen = null;
          }),
        );
    }
    _load();
  }

  @override
  void didUpdateWidget(EmbedPlayer oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.url != widget.url) _load();
  }

  void _load() {
    _update(loading: true);
    _controller.loadRequest(Uri.parse(widget.url));
  }

  void _update({required bool loading, String? error}) {
    if (!mounted) return;
    setState(() {
      _loading = loading;
      _error = error;
    });
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      // Đang fullscreen → nút Back thoát fullscreen trước
      canPop: _fullscreenWidget == null,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop) _exitFullscreen?.call();
      },
      child: Stack(
        fit: StackFit.expand,
        children: [
          WebViewWidget(controller: _controller),
          ?_fullscreenWidget,
          if (_loading && _error == null) const Center(child: CircularProgressIndicator()),
          if (_error != null)
            ColoredBox(
              color: Colors.black,
              child: ErrorView(message: _error!, onRetry: _load),
            ),
        ],
      ),
    );
  }
}
