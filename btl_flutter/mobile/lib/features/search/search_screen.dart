import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/widgets/empty_view.dart';
import '../../data/movie_query.dart';
import '../browse/widgets/movie_grid.dart';
import 'recent_searches.dart';

/// Tìm theo tên: debounce 400ms, tối thiểu 2 ký tự; khi ô trống hiện từ khoá gần đây.
class SearchScreen extends ConsumerStatefulWidget {
  const SearchScreen({super.key});

  static const debounce = Duration(milliseconds: 400);
  static const minLength = 2;

  @override
  ConsumerState<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends ConsumerState<SearchScreen> {
  final _controller = TextEditingController();
  final _focusNode = FocusNode();
  Timer? _debounce;
  String _keyword = '';

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  void _onChanged(String text) {
    _debounce?.cancel();
    _debounce = Timer(SearchScreen.debounce, () => _apply(text));
  }

  void _apply(String text) {
    _debounce?.cancel();
    if (mounted) setState(() => _keyword = text.trim());
  }

  void _submit(String text) {
    _apply(text);
    ref.read(recentSearchesProvider.notifier).add(text);
  }

  void _useRecent(String keyword) {
    _controller.text = keyword;
    _focusNode.unfocus();
    _submit(keyword);
  }

  void _clear() {
    _controller.clear();
    _apply('');
    _focusNode.requestFocus();
  }

  @override
  Widget build(BuildContext context) {
    final searching = _keyword.length >= SearchScreen.minLength;

    return Scaffold(
      appBar: AppBar(
        titleSpacing: 8,
        title: TextField(
          controller: _controller,
          focusNode: _focusNode,
          textInputAction: TextInputAction.search,
          onChanged: _onChanged,
          onSubmitted: _submit,
          decoration: InputDecoration(
            hintText: 'Tìm tên phim...',
            prefixIcon: const Icon(Icons.search),
            suffixIcon: ValueListenableBuilder(
              valueListenable: _controller,
              builder: (context, value, _) => value.text.isEmpty
                  ? const SizedBox.shrink()
                  : IconButton(tooltip: 'Xoá', onPressed: _clear, icon: const Icon(Icons.close)),
            ),
            filled: true,
            isDense: true,
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(24), borderSide: BorderSide.none),
          ),
        ),
      ),
      body: searching
          ? MovieGrid(
              key: ValueKey(_keyword),
              query: MovieQuery.search(_keyword),
              emptyMessage: 'Không tìm thấy phim nào cho "$_keyword"',
              onMovieOpened: (_) => ref.read(recentSearchesProvider.notifier).add(_keyword),
            )
          : _RecentSearches(onSelected: _useRecent, tooShort: _keyword.isNotEmpty),
    );
  }
}

class _RecentSearches extends ConsumerWidget {
  const _RecentSearches({required this.onSelected, required this.tooShort});

  final ValueChanged<String> onSelected;
  final bool tooShort;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final recent = ref.watch(recentSearchesProvider);
    final notifier = ref.read(recentSearchesProvider.notifier);

    if (tooShort) {
      return const EmptyView(icon: Icons.keyboard, message: 'Nhập ít nhất ${SearchScreen.minLength} ký tự');
    }
    if (recent.isEmpty) {
      return const EmptyView(icon: Icons.search, message: 'Nhập tên phim để tìm kiếm');
    }

    return ListView(
      children: [
        ListTile(
          title: Text('Tìm kiếm gần đây', style: Theme.of(context).textTheme.titleSmall),
          trailing: TextButton(onPressed: notifier.clear, child: const Text('Xoá tất cả')),
        ),
        for (final keyword in recent)
          ListTile(
            leading: const Icon(Icons.history),
            title: Text(keyword),
            onTap: () => onSelected(keyword),
            trailing: IconButton(
              tooltip: 'Xoá',
              onPressed: () => notifier.remove(keyword),
              icon: const Icon(Icons.close, size: 20),
            ),
          ),
      ],
    );
  }
}
