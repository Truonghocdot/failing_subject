import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/widgets/movie_card.dart';

import '../support/fakes.dart';

void main() {
  Future<void> openDetail(WidgetTester tester, FakeMovieRepository repo, String name) async {
    await pumpApp(tester, repo);
    await tester.tap(find.descendant(of: find.byType(MovieCard), matching: find.text(name)));
    await tester.pumpAndSettle();
  }

  final repo = FakeMovieRepository(details: {sampleSeries.slug: sampleSeries, sampleSingle.slug: sampleSingle});

  testWidgets('Phim bộ: thông tin, server, lưới tập', (tester) async {
    await openDetail(tester, repo, 'Mùa Hè Đáng Nhớ');

    expect(find.text('Mùa Hè Đáng Nhớ'), findsOneWidget);
    expect(find.text('Summer Vacation'), findsOneWidget);
    expect(find.text('Tập 3'), findsOneWidget); // tag episodeCurrent
    expect(find.text('Đang chiếu'), findsOneWidget);
    expect(find.text('Chính Kịch'), findsOneWidget);
    expect(find.textContaining('Hàn Quốc'), findsOneWidget);
    expect(find.text('Xem Tập 1'), findsOneWidget);
    expect(find.text('Danh sách tập (3)'), findsOneWidget);
    expect(find.byType(NavigationBar), findsNothing); // trang chi tiết che bottom nav

    // Đổi server → lưới tập đổi theo
    await tester.tap(find.text('Thuyết minh #1'));
    await tester.pumpAndSettle();
    expect(find.text('Danh sách tập (1)'), findsOneWidget);
  });

  testWidgets('Bấm chip thể loại / quốc gia → lưới phim đã lọc', (tester) async {
    await openDetail(tester, repo, 'Mùa Hè Đáng Nhớ');

    await tester.tap(find.widgetWithText(ActionChip, 'Chính Kịch'));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(AppBar, 'Chính Kịch'), findsOneWidget);
    expect(find.text('Phim genre-chinh-kich 1.1'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(ActionChip, 'Hàn Quốc'));
    await tester.pumpAndSettle();
    expect(find.text('Phim country-han-quoc 1.1'), findsOneWidget);
  });

  testWidgets('Phim lẻ: chỉ có nút Xem phim, không có lưới tập', (tester) async {
    await openDetail(tester, repo, 'La chimera');

    expect(find.text('Xem phim'), findsOneWidget);
    expect(find.textContaining('Danh sách tập'), findsNothing);
  });

  testWidgets('Không tìm thấy phim → ErrorView', (tester) async {
    await openDetail(tester, FakeMovieRepository(), 'Sở Hữu');

    expect(find.text('Không tìm thấy nội dung'), findsOneWidget);
    expect(find.text('Thử lại'), findsOneWidget);
  });
}
