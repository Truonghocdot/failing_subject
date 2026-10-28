import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../data/models/movie_summary.dart';
import 'app_network_image.dart';
import 'rating_badge.dart';

/// Thẻ phim: poster 2:3, tên 2 dòng, năm. Bấm → trang chi tiết.
class MovieCard extends StatelessWidget {
  const MovieCard({super.key, required this.movie, this.width = 112, this.onOpened, this.subtitle});

  /// Dòng dưới tên phim; mặc định là năm phát hành.
  final String? subtitle;

  final MovieSummary movie;
  final double width;

  /// Gọi ngay trước khi điều hướng sang trang chi tiết.
  final VoidCallback? onOpened;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SizedBox(
      width: width,
      child: InkWell(
        borderRadius: BorderRadius.circular(8),
        onTap: () {
          onOpened?.call();
          context.push(Routes.movie(movie.slug));
        },
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            AspectRatio(
              aspectRatio: 2 / 3,
              child: ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Stack(
                  fit: StackFit.expand,
                  children: [
                    AppNetworkImage(url: movie.posterUrl, decodeWidth: width),
                    if (movie.rating != null) Positioned(top: 6, left: 6, child: RatingBadge(rating: movie.rating!)),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 6),
            Text(
              movie.name,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w600),
            ),
            if ((subtitle ?? movie.year?.toString()) case final line?)
              Text(
                line,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
              ),
          ],
        ),
      ),
    );
  }
}
