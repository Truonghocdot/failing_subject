import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api_client.dart';
import 'package:mobile/core/widgets/movie_card.dart';
import 'package:mobile/features/home/widgets/home_banner.dart';

import '../support/fakes.dart';

void main() {
  testWidgets('Hiển thị banner và các hàng phim', (tester) async {
    await pumpApp(tester, FakeMovieRepository());

    expect(find.byType(HomeBanner), findsOneWidget);
    expect(find.text('Mới cập nhật'), findsOneWidget);
    expect(find.text('Phim lẻ'), findsOneWidget);
    expect(find.byType(MovieCard), findsNWidgets(3));
    expect(find.text('7.5'), findsOneWidget); // RatingBadge của "Sở Hữu"
  });

  testWidgets('Lỗi → ErrorView, bấm Thử lại tải lại thành công', (tester) async {
    final repo = FakeMovieRepository(error: const AppException('Không kết nối được máy chủ'));
    await pumpApp(tester, repo);

    expect(find.text('Không kết nối được máy chủ'), findsOneWidget);
    expect(find.byType(MovieCard), findsNothing);

    repo.error = null;
    await tester.tap(find.text('Thử lại'));
    await tester.pumpAndSettle();

    expect(find.byType(MovieCard), findsNWidgets(3));
    expect(repo.homeCalls, 2);
  });

  testWidgets('Danh sách rỗng → thông báo', (tester) async {
    await pumpApp(tester, FakeMovieRepository(sections: []));

    expect(find.text('Chưa có phim nào để hiển thị'), findsOneWidget);
  });
}
