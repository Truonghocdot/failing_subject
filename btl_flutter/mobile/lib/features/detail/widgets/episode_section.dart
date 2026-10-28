import 'package:flutter/material.dart';

import '../../../data/models/movie_detail.dart';

/// Chọn server (nếu có nhiều) + lưới tập của server đang chọn.
class EpisodeSection extends StatelessWidget {
  const EpisodeSection({
    super.key,
    required this.servers,
    required this.selectedServer,
    required this.onServerChanged,
    required this.onEpisodeTap,
    this.watchedEpisodeSlug,
  });

  final List<Server> servers;
  final int selectedServer;

  /// Tập xem gần nhất trên server đang chọn – được tô nổi.
  final String? watchedEpisodeSlug;
  final ValueChanged<int> onServerChanged;
  final ValueChanged<Episode> onEpisodeTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final episodes = servers[selectedServer].episodes;

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 24, 16, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Danh sách tập (${episodes.length})',
            style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold),
          ),
          if (servers.length > 1) ...[
            const SizedBox(height: 10),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (var i = 0; i < servers.length; i++)
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: Text(servers[i].name),
                        selected: i == selectedServer,
                        onSelected: (_) => onServerChanged(i),
                      ),
                    ),
                ],
              ),
            ),
          ],
          const SizedBox(height: 12),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final episode in episodes)
                episode.slug == watchedEpisodeSlug
                    ? FilledButton.tonal(
                        style: FilledButton.styleFrom(minimumSize: const Size(64, 40)),
                        onPressed: () => onEpisodeTap(episode),
                        child: Text(episode.name),
                      )
                    : OutlinedButton(
                        style: OutlinedButton.styleFrom(
                          minimumSize: const Size(64, 40),
                          padding: const EdgeInsets.symmetric(horizontal: 12),
                        ),
                        onPressed: () => onEpisodeTap(episode),
                        child: Text(episode.name),
                      ),
            ],
          ),
        ],
      ),
    );
  }
}
