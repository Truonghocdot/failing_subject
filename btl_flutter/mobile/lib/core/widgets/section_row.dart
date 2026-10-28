import 'package:flutter/material.dart';

import '../../data/models/movie_summary.dart';
import 'movie_card.dart';

/// Hàng phim cuộn ngang có tiêu đề + nút "Xem tất cả" (nếu có [onSeeAll]).
class SectionRow extends StatelessWidget {
  const SectionRow({
    super.key,
    required this.title,
    required this.movies,
    this.cardWidth = 112,
    this.onSeeAll,
    this.subtitles,
  });

  final String title;
  final List<MovieSummary> movies;
  final double cardWidth;
  final VoidCallback? onSeeAll;

  /// Dòng phụ cho từng thẻ (cùng thứ tự [movies]) thay cho năm – VD "Tập 3" ở hàng Xem tiếp.
  final List<String>? subtitles;

  @override
  Widget build(BuildContext context) {
    // poster 2:3 + 2 dòng tên + năm
    final rowHeight = cardWidth * 1.5 + 64;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 8, 4),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  title,
                  style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
                ),
              ),
              if (onSeeAll != null) TextButton(onPressed: onSeeAll, child: const Text('Xem tất cả')),
            ],
          ),
        ),
        SizedBox(
          height: rowHeight,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 16),
            itemCount: movies.length,
            separatorBuilder: (context, index) => const SizedBox(width: 12),
            itemBuilder: (context, index) =>
                MovieCard(movie: movies[index], width: cardWidth, subtitle: subtitles?.elementAtOrNull(index)),
          ),
        ),
      ],
    );
  }
}
