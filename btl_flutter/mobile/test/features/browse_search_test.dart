import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/widgets/movie_card.dart';
import 'package:mobile/features/search/recent_searches.dart';
import 'package:mobile/features/search/search_screen.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../support/fakes.dart';

void main() {
  group('Khám phá', () {
    testWidgets('Hiện danh sách, thể loại, quốc gia, năm', (tester) async {
      await pumpApp(tester, FakeMovieRepository());
      await tester.tap(find.text('Khám phá'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(ActionChip, 'Phim bộ'), findsOneWidget);
      expect(find.widgetWithText(ActionChip, 'Lãng Mạn'), findsOneWidget); // từ API thể loại
      expect(find.widgetWithText(ActionChip, 'Hàn Quốc'), findsOneWidget);
      expect(find.widgetWithText(ActionChip, '${DateTime.now().year}'), findsOneWidget);
    });

    testWidgets('Bấm "Phim bộ" → lưới phim, bottom nav bị che', (tester) async {
      await pumpApp(tester, FakeMovieRepository());
      await tester.tap(find.text('Khám phá'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ActionChip, 'Phim bộ'));
      await tester.pumpAndSettle();

      expect(find.widgetWithText(AppBar, 'Phim bộ'), findsOneWidget);
      expect(find.byType(MovieCard), findsNWidgets(3));
      expect(find.byType(NavigationBar), findsNothing);
    });

    testWidgets('Tất cả quốc gia → bottom sheet', (tester) async {
      final repo = FakeMovieRepository();
      await pumpApp(tester, repo);
      await tester.tap(find.text('Khám phá'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Tất cả quốc gia'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(ListTile, 'Hàn Quốc'));
      await tester.pumpAndSettle();

      expect(repo.pagedCalls, contains('country-han-quoc:1'));
    });

    testWidgets('"Xem tất cả" ở trang chủ → danh sách tương ứng', (tester) async {
      final repo = FakeMovieRepository();
      await pumpApp(tester, repo);
      await tester.tap(find.text('Xem tất cả').last); // hàng "Phim lẻ"
      await tester.pumpAndSettle();

      expect(find.widgetWithText(AppBar, 'Phim lẻ'), findsOneWidget);
      expect(repo.pagedCalls, ['list-phim-le:1']);
    });
  });

  group('Tìm kiếm', () {
    Future<void> openSearch(WidgetTester tester, {Map<String, Object> prefs = const {}}) async {
      await pumpApp(tester, FakeMovieRepository(), prefs: prefs);
      await tester.tap(find.text('Tìm kiếm'));
      await tester.pumpAndSettle();
    }

    testWidgets('Gõ từ khoá → debounce rồi hiện kết quả; mở phim thì lưu từ khoá', (tester) async {
      await openSearch(tester);
      expect(find.text('Nhập tên phim để tìm kiếm'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'mua');
      await tester.pump(const Duration(milliseconds: 100));
      expect(find.byType(MovieCard), findsNothing); // chưa hết debounce

      await tester.pump(SearchScreen.debounce);
      await tester.pumpAndSettle();
      expect(find.text('Mùa Hè Đáng Nhớ'), findsOneWidget);

      await tester.tap(find.byType(MovieCard));
      await tester.pumpAndSettle();
      final prefs = await SharedPreferences.getInstance();
      expect(prefs.getStringList(RecentSearchesNotifier.storageKey), ['mua']);
    });

    testWidgets('1 ký tự → nhắc nhập thêm; không có kết quả → thông báo', (tester) async {
      await openSearch(tester);

      await tester.enterText(find.byType(TextField), 'm');
      await tester.pump(SearchScreen.debounce);
      expect(find.text('Nhập ít nhất 2 ký tự'), findsOneWidget);

      await tester.enterText(find.byType(TextField), 'zzz');
      await tester.pump(SearchScreen.debounce);
      await tester.pumpAndSettle();
      expect(find.text('Không tìm thấy phim nào cho "zzz"'), findsOneWidget);
    });

    testWidgets('Từ khoá gần đây: bấm để tìm lại, xoá được', (tester) async {
      await openSearch(
        tester,
        prefs: {
          RecentSearchesNotifier.storageKey: ['mua he', 'so huu'],
        },
      );

      expect(find.text('Tìm kiếm gần đây'), findsOneWidget);
      await tester.tap(find.text('mua he'));
      await tester.pumpAndSettle();
      expect(find.text('Mùa Hè Đáng Nhớ'), findsOneWidget);

      await tester.tap(find.byTooltip('Xoá').first); // nút xoá trong ô tìm kiếm
      await tester.pumpAndSettle();
      await tester.tap(find.text('Xoá tất cả'));
      await tester.pumpAndSettle();
      expect(find.text('Nhập tên phim để tìm kiếm'), findsOneWidget);
    });
  });
}
