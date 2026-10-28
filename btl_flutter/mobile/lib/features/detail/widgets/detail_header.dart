import 'package:flutter/material.dart';

import '../../../core/widgets/app_network_image.dart';
import '../../../core/widgets/rating_badge.dart';
import '../../../data/models/movie_detail.dart';

/// Poster + tên + các nhãn thông tin (năm, chất lượng, ngôn ngữ, tập, thời lượng).
class DetailHeader extends StatelessWidget {
  const DetailHeader({super.key, required this.movie});

  static const _posterWidth = 110.0;

  final MovieDetail movie;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final tags = [
      movie.year?.toString(),
      movie.quality,
      movie.lang,
      movie.episodeCurrent,
      movie.duration,
      movie.statusLabel,
    ].whereType<String>().toList();

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(8),
            child: SizedBox(
              width: _posterWidth,
              height: _posterWidth * 1.5,
              child: AppNetworkImage(url: movie.posterUrl, decodeWidth: _posterWidth),
            ),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(movie.name, style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold)),
                if (movie.originName != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 4),
                    child: Text(
                      movie.originName!,
                      style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                  ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    if (movie.rating != null) RatingBadge(rating: movie.rating!),
                    for (final tag in tags) _Tag(tag),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border.all(color: scheme.outlineVariant),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
        child: Text(text, style: Theme.of(context).textTheme.labelSmall),
      ),
    );
  }
}
