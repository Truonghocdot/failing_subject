import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fakes.dart';

void main() {
  testWidgets('Hiển thị 4 tab và chuyển tab được', (tester) async {
    await pumpApp(tester, FakeMovieRepository());

    expect(find.byType(NavigationBar), findsOneWidget);
    expect(find.byType(NavigationDestination), findsNWidgets(4));
    expect(find.text('VsMovie'), findsOneWidget); // AppBar trang chủ

    await tester.tap(find.text('Tìm kiếm'));
    await tester.pumpAndSettle();
    expect(find.text('Nhập tên phim để tìm kiếm'), findsOneWidget);

    await tester.tap(find.text('Khám phá'));
    await tester.pumpAndSettle();
    expect(find.text('Thể loại'), findsOneWidget);

    await tester.tap(find.text('Thư viện'));
    await tester.pumpAndSettle();
    expect(find.text('Lưu phim yêu thích & xem tiếp'), findsOneWidget); // khách
  });
}
