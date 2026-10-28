import 'package:cached_network_image/cached_network_image.dart';
import 'package:flutter/material.dart';

/// Ảnh mạng có cache. Ảnh gốc của nguồn rất nặng (~1.5 MB) nên luôn giới hạn kích thước decode
/// theo chiều rộng hiển thị thực tế ([decodeWidth] logical px × devicePixelRatio).
class AppNetworkImage extends StatelessWidget {
  const AppNetworkImage({super.key, required this.url, required this.decodeWidth, this.fit = BoxFit.cover});

  final String url;
  final double decodeWidth;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    if (url.isEmpty) return const _ImageFallback();

    final pixelRatio = MediaQuery.devicePixelRatioOf(context);
    return CachedNetworkImage(
      imageUrl: url,
      fit: fit,
      memCacheWidth: (decodeWidth * pixelRatio).round(),
      fadeInDuration: const Duration(milliseconds: 200),
      placeholder: (context, url) => const _ImagePlaceholder(),
      errorWidget: (context, url, error) => const _ImageFallback(),
    );
  }
}

class _ImagePlaceholder extends StatelessWidget {
  const _ImagePlaceholder();

  @override
  Widget build(BuildContext context) => ColoredBox(color: Theme.of(context).colorScheme.surfaceContainerHighest);
}

class _ImageFallback extends StatelessWidget {
  const _ImageFallback();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return ColoredBox(
      color: scheme.surfaceContainerHighest,
      child: Center(child: Icon(Icons.movie_outlined, color: scheme.onSurfaceVariant)),
    );
  }
}
