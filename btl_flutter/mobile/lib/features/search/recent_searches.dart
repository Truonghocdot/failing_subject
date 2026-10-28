import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/preferences.dart';

/// Từ khoá tìm kiếm gần đây (tối đa [maxItems], mới nhất trước), lưu bằng shared_preferences.
class RecentSearchesNotifier extends Notifier<List<String>> {
  static const storageKey = 'recent_searches';
  static const maxItems = 10;

  @override
  List<String> build() => ref.watch(sharedPreferencesProvider).getStringList(storageKey) ?? const [];

  void add(String keyword) {
    final value = keyword.trim();
    if (value.length < 2) return;
    final lower = value.toLowerCase();
    _save([value, ...state.where((e) => e.toLowerCase() != lower)].take(maxItems).toList());
  }

  void remove(String keyword) => _save(state.where((e) => e != keyword).toList());

  void clear() => _save(const []);

  void _save(List<String> next) {
    state = next;
    ref.read(sharedPreferencesProvider).setStringList(storageKey, next);
  }
}

final recentSearchesProvider = NotifierProvider<RecentSearchesNotifier, List<String>>(RecentSearchesNotifier.new);
