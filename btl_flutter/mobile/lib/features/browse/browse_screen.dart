import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/router.dart';
import '../../core/api_client.dart';
import '../../data/models/taxonomy.dart';
import '../../data/movie_query.dart';
import 'browse_providers.dart';

/// Tab Khám phá: chọn danh sách / thể loại / quốc gia / năm → mở lưới phim đã lọc.
class BrowseScreen extends ConsumerWidget {
  const BrowseScreen({super.key});

  static const _yearCount = 15;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final currentYear = DateTime.now().year;
    void open(MovieQuery query, String title) => context.push(Routes.list(query, title: title));

    return Scaffold(
      appBar: AppBar(title: const Text('Khám phá')),
      body: ListView(
        padding: const EdgeInsets.only(bottom: 32),
        children: [
          const _SectionTitle('Danh sách'),
          _ChipWrap(
            children: [
              for (final MapEntry(key: slug, value: title) in movieLists.entries)
                ActionChip(label: Text(title), onPressed: () => open(MovieQuery.list(slug), title)),
            ],
          ),
          const _SectionTitle('Thể loại'),
          _AsyncChips(
            value: ref.watch(genresProvider),
            onRetry: () => ref.invalidate(genresProvider),
            onSelected: (genre) => open(MovieQuery.genre(genre.slug), genre.name),
          ),
          const _SectionTitle('Quốc gia'),
          _ChipWrap(
            children: [
              for (final country in popularCountries)
                ActionChip(
                  label: Text(country.name),
                  onPressed: () => open(MovieQuery.country(country.slug), country.name),
                ),
              ActionChip(
                avatar: const Icon(Icons.public, size: 18),
                label: const Text('Tất cả quốc gia'),
                onPressed: () => _showAllCountries(context, open),
              ),
            ],
          ),
          const _SectionTitle('Năm phát hành'),
          _ChipWrap(
            children: [
              for (var year = currentYear; year > currentYear - _yearCount; year--)
                ActionChip(label: Text('$year'), onPressed: () => open(MovieQuery.year(year), 'Phim năm $year')),
            ],
          ),
        ],
      ),
    );
  }

  void _showAllCountries(BuildContext context, void Function(MovieQuery, String) open) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) => DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.7,
        maxChildSize: 0.95,
        builder: (context, scrollController) => Consumer(
          builder: (context, ref, _) => ref
              .watch(countriesProvider)
              .when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error: (error, _) => Center(child: Text(errorMessage(error))),
                data: (countries) => ListView.builder(
                  controller: scrollController,
                  itemCount: countries.length,
                  itemBuilder: (context, index) {
                    final country = countries[index];
                    return ListTile(
                      title: Text(country.name),
                      onTap: () {
                        Navigator.pop(sheetContext);
                        open(MovieQuery.country(country.slug), country.name);
                      },
                    );
                  },
                ),
              ),
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(16, 20, 16, 10),
    child: Text(text, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.bold)),
  );
}

class _ChipWrap extends StatelessWidget {
  const _ChipWrap({required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(horizontal: 16),
    child: Wrap(spacing: 8, runSpacing: 8, children: children),
  );
}

class _AsyncChips extends StatelessWidget {
  const _AsyncChips({required this.value, required this.onRetry, required this.onSelected});

  final AsyncValue<List<Taxonomy>> value;
  final VoidCallback onRetry;
  final ValueChanged<Taxonomy> onSelected;

  @override
  Widget build(BuildContext context) {
    return value.when(
      loading: () => const Padding(
        padding: EdgeInsets.all(16),
        child: Center(child: CircularProgressIndicator()),
      ),
      error: (error, _) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16),
        child: Row(
          children: [
            Expanded(child: Text(errorMessage(error))),
            TextButton(onPressed: onRetry, child: const Text('Thử lại')),
          ],
        ),
      ),
      data: (items) => _ChipWrap(
        children: [for (final item in items) ActionChip(label: Text(item.name), onPressed: () => onSelected(item))],
      ),
    );
  }
}
