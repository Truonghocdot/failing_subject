import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/app.dart';

void main() {
  testWidgets('Hiển thị 4 tab và chuyển tab được', (tester) async {
    await tester.pumpWidget(const ProviderScope(child: VsMovieApp()));
    await tester.pumpAndSettle();

    expect(find.byType(NavigationBar), findsOneWidget);
    expect(find.byType(NavigationDestination), findsNWidgets(4));
    expect(find.text('VsMovie'), findsOneWidget); // AppBar trang chủ

    await tester.tap(find.text('Tìm kiếm'));
    await tester.pumpAndSettle();
    expect(find.text('Tìm phim theo tên – Phase 4'), findsOneWidget);

    await tester.tap(find.text('Thư viện'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Phase 6'), findsOneWidget);
  });
}
