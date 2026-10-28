import 'package:flutter/material.dart';

import '../../data/models/movie_detail.dart';

typedef PickedEpisode = ({int serverIndex, Episode episode});

/// Bottom sheet chọn server + tập ngay trong trình phát.
Future<PickedEpisode?> showEpisodePickerSheet(
  BuildContext context, {
  required List<Server> servers,
  required int serverIndex,
  required String episodeSlug,
}) {
  return showModalBottomSheet<PickedEpisode>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (context) => _EpisodePicker(servers: servers, initialServer: serverIndex, currentEpisode: episodeSlug),
  );
}

class _EpisodePicker extends StatefulWidget {
  const _EpisodePicker({required this.servers, required this.initialServer, required this.currentEpisode});

  final List<Server> servers;
  final int initialServer;
  final String currentEpisode;

  @override
  State<_EpisodePicker> createState() => _EpisodePickerState();
}

class _EpisodePickerState extends State<_EpisodePicker> {
  late int _server = widget.initialServer;

  @override
  Widget build(BuildContext context) {
    final episodes = widget.servers[_server].episodes;
    final maxHeight = MediaQuery.sizeOf(context).height * 0.8;

    return ConstrainedBox(
      constraints: BoxConstraints(maxHeight: maxHeight),
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (widget.servers.length > 1)
              Wrap(
                spacing: 8,
                children: [
                  for (var i = 0; i < widget.servers.length; i++)
                    ChoiceChip(
                      label: Text(widget.servers[i].name),
                      selected: i == _server,
                      onSelected: (_) => setState(() => _server = i),
                    ),
                ],
              ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                for (final episode in episodes)
                  _server == widget.initialServer && episode.slug == widget.currentEpisode
                      ? FilledButton(onPressed: () => Navigator.pop(context), child: Text(episode.name))
                      : OutlinedButton(
                          onPressed: () =>
                              Navigator.pop<PickedEpisode>(context, (serverIndex: _server, episode: episode)),
                          child: Text(episode.name),
                        ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
