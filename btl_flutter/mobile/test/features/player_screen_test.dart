import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/widgets/movie_card.dart';
import 'package:mobile/features/detail/detail_screen.dart';
import 'package:mobile/features/player/embed_player.dart';
import 'package:mobile/features/player/player_screen.dart';

import '../support/fakes.dart';

void main() {
  final repo = FakeMovieRepository(details: {sampleSeries.slug: sampleSeries, sampleSingle.slug: sampleSingle});

  Future<void> openMovie(WidgetTester tester, String name) async {
    await pumpApp(tester, repo);
    await tester.tap(find.descendant(of: find.byType(MovieCard), matching: find.text(name)));
    await tester.pumpAndSettle();
  }

  IconButton iconButton(WidgetTester tester, IconData icon) =>
      tester.widget<IconButton>(find.widgetWithIcon(IconButton, icon));

  testWidgets('Bấm tập ở trang chi tiết → phát đúng embedUrl, chuyển tập trước/sau', (tester) async {
    await openMovie(tester, 'Mùa Hè Đáng Nhớ');
    await tester.tap(find.widgetWithText(OutlinedButton, '2'));
    await tester.pumpAndSettle();

    expect(find.byType(PlayerScreen), findsOneWidget);
    expect(find.text('EMBED https://v1.streamvsmov.com/video/2'), findsOneWidget);
    expect(find.text('Tập 2 • Vietsub #1'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.skip_next));
    await tester.pump();
    expect(find.text('EMBED https://v1.streamvsmov.com/video/3'), findsOneWidget);
    expect(iconButton(tester, Icons.skip_next).onPressed, isNull); // tập cuối

    await tester.tap(find.byIcon(Icons.skip_previous));
    await tester.pump();
    expect(find.text('Tập 2 • Vietsub #1'), findsOneWidget);

    // Quay lại trang chi tiết
    await tester.tap(find.byIcon(Icons.arrow_back));
    await tester.pumpAndSettle();
    expect(find.byType(DetailScreen), findsOneWidget);
  });

  testWidgets('Chọn tập ở server khác qua bottom sheet', (tester) async {
    await openMovie(tester, 'Mùa Hè Đáng Nhớ');
    await tester.tap(find.text('Xem Tập 1'));
    await tester.pumpAndSettle();
    expect(iconButton(tester, Icons.skip_previous).onPressed, isNull); // tập đầu

    await tester.tap(find.byIcon(Icons.playlist_play));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ChoiceChip, 'Thuyết minh #1'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(OutlinedButton, '1'));
    await tester.pumpAndSettle();

    expect(find.text('EMBED https://v2.streamvsmov.com/video/1'), findsOneWidget);
    expect(find.text('Tập 1 • Thuyết minh #1'), findsOneWidget);
  });

  testWidgets('Thanh điều khiển tự ẩn sau 4 giây, bấm nút để hiện lại', (tester) async {
    await openMovie(tester, 'La chimera');
    await tester.tap(find.text('Xem phim'));
    await tester.pumpAndSettle();
    expect(find.text('EMBED https://v14.streamvsmov.com/video/x'), findsOneWidget);
    expect(find.byTooltip('Hiện điều khiển'), findsNothing);

    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Hiện điều khiển'), findsOneWidget);

    await tester.tap(find.byTooltip('Hiện điều khiển'));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Hiện điều khiển'), findsNothing);
  });

  testWidgets('Tập không tồn tại → thông báo lỗi', (tester) async {
    await pumpApp(tester, repo);
    final context = tester.element(find.byType(MovieCard).first);
    // ignore: use_build_context_synchronously
    Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => const PlayerScreen(slug: 'chimera', serverIndex: 0, episodeSlug: 'tap-99'),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Không tìm thấy tập phim'), findsOneWidget);
  });

  group('isAllowedPlayerNavigation', () {
    bool allowed(String url, {bool main = true}) =>
        isAllowedPlayerNavigation(url, isMainFrame: main, hostSuffix: 'streamvsmov.com');

    test('cho phép domain trình phát', () {
      expect(allowed('https://v1.streamvsmov.com/video/abc'), isTrue);
      expect(allowed('https://streamvsmov.com/x'), isTrue);
      expect(allowed('about:blank'), isTrue);
    });

    test('chặn quảng cáo / scheme lạ ở khung chính', () {
      expect(allowed('https://ads.example.com/click'), isFalse);
      expect(allowed('https://evilstreamvsmov.com/x'), isFalse);
      expect(allowed('intent://open#Intent;end'), isFalse);
      expect(allowed('market://details?id=x'), isFalse);
    });

    test('không chặn iframe con', () {
      expect(allowed('https://ads.example.com/frame', main: false), isTrue);
    });
  });
}
