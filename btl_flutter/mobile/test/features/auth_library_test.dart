import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/widgets/movie_card.dart';
import 'package:mobile/features/auth/login_screen.dart';
import 'package:mobile/features/detail/detail_screen.dart';
import 'package:mobile/features/library/widgets/history_list.dart';

import '../support/fakes.dart';

void main() {
  final movies = FakeMovieRepository(details: {sampleSeries.slug: sampleSeries, sampleSingle.slug: sampleSingle});

  Future<void> openTab(WidgetTester tester, String label) async {
    await tester.tap(find.descendant(of: find.byType(NavigationBar), matching: find.text(label)));
    await tester.pumpAndSettle();
  }

  Future<void> openMovie(WidgetTester tester, String name) async {
    await tester.tap(find.descendant(of: find.byType(MovieCard), matching: find.text(name)).first);
    await tester.pumpAndSettle();
  }

  group('Đăng nhập / đăng ký', () {
    testWidgets('Khách → Đăng nhập → Thư viện hiện tài khoản; đăng xuất về khách', (tester) async {
      final deps = await pumpApp(tester, movies);
      await openTab(tester, 'Thư viện');
      await tester.tap(find.text('Đăng nhập'));
      await tester.pumpAndSettle();
      expect(find.byType(LoginScreen), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), sampleUser.email);
      await tester.enterText(find.widgetWithText(TextFormField, 'Mật khẩu'), samplePassword);
      await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
      await tester.pumpAndSettle();

      expect(find.byType(LoginScreen), findsNothing);
      expect(find.text('An Nguyễn'), findsOneWidget);
      expect(find.text('Yêu thích'), findsOneWidget);
      expect(deps.storage.tokens?.accessToken, 'access-1');

      await tester.tap(find.byTooltip('Đăng xuất'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Đăng xuất'));
      await tester.pumpAndSettle();

      expect(find.text('Lưu phim yêu thích & xem tiếp'), findsOneWidget);
      expect(deps.storage.tokens, isNull);
      expect(deps.auth.loggedOutTokens, ['refresh-1']);
    });

    testWidgets('Sai mật khẩu → hiện lỗi từ server, vẫn ở màn đăng nhập', (tester) async {
      await pumpApp(tester, movies);
      await openTab(tester, 'Thư viện');
      await tester.tap(find.text('Đăng nhập'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), sampleUser.email);
      await tester.enterText(find.widgetWithText(TextFormField, 'Mật khẩu'), 'sai-mat-khau');
      await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
      await tester.pumpAndSettle();

      expect(find.text('Email hoặc mật khẩu không đúng'), findsOneWidget);
      expect(find.byType(LoginScreen), findsOneWidget);
    });

    testWidgets('Đăng ký: kiểm tra tại chỗ, email trùng báo lỗi, hợp lệ thì đăng nhập luôn', (tester) async {
      await pumpApp(tester, movies);
      await openTab(tester, 'Thư viện');
      await tester.tap(find.text('Tạo tài khoản'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FilledButton, 'Đăng ký'));
      await tester.pumpAndSettle();
      expect(find.text('Vui lòng nhập tên hiển thị'), findsOneWidget);
      expect(find.text('Email không hợp lệ'), findsOneWidget);
      expect(find.text('Mật khẩu tối thiểu 8 ký tự'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextFormField, 'Tên hiển thị'), 'Bình');
      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), sampleUser.email);
      await tester.enterText(find.widgetWithText(TextFormField, 'Mật khẩu'), 'matkhau456');
      await tester.enterText(find.widgetWithText(TextFormField, 'Nhập lại mật khẩu'), 'matkhau456');
      await tester.tap(find.widgetWithText(FilledButton, 'Đăng ký'));
      await tester.pumpAndSettle();
      expect(find.text('Email đã được sử dụng'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'binh@vsmovie.vn');
      await tester.tap(find.widgetWithText(FilledButton, 'Đăng ký'));
      await tester.pumpAndSettle();
      expect(find.text('Bình'), findsOneWidget);
      expect(find.text('binh@vsmovie.vn'), findsOneWidget);
    });

    testWidgets('Bấm "Tiếp" trên bàn phím nhảy qua từng ô, không dừng ở nút ẩn/hiện mật khẩu', (tester) async {
      await pumpApp(tester, movies);
      await openTab(tester, 'Thư viện');
      await tester.tap(find.text('Tạo tài khoản'));
      await tester.pumpAndSettle();

      EditableText editable(String label) => tester.widget<EditableText>(
        find.descendant(of: find.widgetWithText(TextFormField, label), matching: find.byType(EditableText)),
      );

      await tester.tap(find.widgetWithText(TextFormField, 'Mật khẩu'));
      await tester.pump();
      await tester.testTextInput.receiveAction(TextInputAction.next);
      await tester.pump();

      expect(editable('Nhập lại mật khẩu').focusNode.hasFocus, isTrue);
    });

    testWidgets('Sau lần gửi lỗi, sửa đúng thì thông báo lỗi tự mất', (tester) async {
      await pumpApp(tester, movies);
      await openTab(tester, 'Thư viện');
      await tester.tap(find.text('Đăng nhập'));
      await tester.pumpAndSettle();

      await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
      await tester.pumpAndSettle();
      expect(find.text('Email không hợp lệ'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), sampleUser.email);
      await tester.pump();
      expect(find.text('Email không hợp lệ'), findsNothing);
    });

    testWidgets('Mở app khi đã có token → khôi phục phiên', (tester) async {
      await pumpApp(tester, movies, signedIn: true);
      await openTab(tester, 'Thư viện');

      expect(find.text('An Nguyễn'), findsOneWidget);
    });
  });

  group('Yêu thích', () {
    testWidgets('Khách bấm ♥ → mở đăng nhập, đăng nhập xong quay lại trang phim', (tester) async {
      await pumpApp(tester, movies);
      await openMovie(tester, 'Mùa Hè Đáng Nhớ');
      await tester.tap(find.byTooltip('Đăng nhập để lưu phim yêu thích'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextFormField, 'Email'), sampleUser.email);
      await tester.enterText(find.widgetWithText(TextFormField, 'Mật khẩu'), samplePassword);
      await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
      await tester.pumpAndSettle();

      expect(find.byType(DetailScreen), findsOneWidget);
      expect(find.byTooltip('Thêm vào yêu thích'), findsOneWidget);
    });

    testWidgets('Bấm ♥ → lưu, tab Yêu thích hiện phim; bấm lại → bỏ', (tester) async {
      final deps = await pumpApp(tester, movies, signedIn: true);
      await openMovie(tester, 'Mùa Hè Đáng Nhớ');

      await tester.tap(find.byTooltip('Thêm vào yêu thích'));
      await tester.pumpAndSettle();
      expect(find.byTooltip('Bỏ yêu thích'), findsOneWidget);
      expect(deps.library.favorites.single.slug, sampleSeries.slug);

      await tester.pageBack();
      await tester.pumpAndSettle();
      await openTab(tester, 'Thư viện');
      expect(find.descendant(of: find.byType(MovieCard), matching: find.text('Mùa Hè Đáng Nhớ')), findsOneWidget);

      await openMovie(tester, 'Mùa Hè Đáng Nhớ');
      await tester.tap(find.byTooltip('Bỏ yêu thích'));
      await tester.pumpAndSettle();
      expect(deps.library.favorites, isEmpty);
      await tester.pageBack();
      await tester.pumpAndSettle();
      expect(find.textContaining('Chưa có phim yêu thích'), findsOneWidget);
    });
  });

  group('Lịch sử xem', () {
    testWidgets('Xem tập 2 → ghi lịch sử; trang chi tiết hiện "Xem tiếp Tập 2"; trang chủ có hàng Xem tiếp', (
      tester,
    ) async {
      final deps = await pumpApp(tester, movies, signedIn: true);
      expect(find.text('Xem tiếp'), findsNothing);

      await openMovie(tester, 'Mùa Hè Đáng Nhớ');
      await tester.tap(find.widgetWithText(OutlinedButton, '2'));
      await tester.pumpAndSettle();
      expect(deps.library.savedHistory, ['mua-he-dang-nho-2026/Vietsub #1/tap-2']);

      await tester.tap(find.byIcon(Icons.arrow_back));
      await tester.pumpAndSettle();
      expect(find.text('Xem tiếp Tập 2'), findsOneWidget);
      expect(find.widgetWithText(FilledButton, '2'), findsOneWidget); // tập đã xem được tô nổi

      await tester.pageBack();
      await tester.pumpAndSettle();
      expect(find.text('Xem tiếp'), findsOneWidget);
      expect(find.text('Tập 2'), findsOneWidget);
    });

    testWidgets('"Xem tiếp" mở đúng server + tập đã lưu', (tester) async {
      final library = FakeLibraryRepository()
        ..history.add(
          FakeLibraryRepository.historyEntry(
            sampleSeries.slug,
            serverName: 'Thuyết minh #1',
            episodeSlug: 'tap-1',
            episodeName: '1',
          ),
        );
      await pumpApp(tester, movies, signedIn: true, library: library);
      await openMovie(tester, 'Mùa Hè Đáng Nhớ');

      expect(find.text('Xem tiếp Tập 1'), findsOneWidget);
      expect(find.text('Danh sách tập (1)'), findsOneWidget); // đang chọn server Thuyết minh
      await tester.tap(find.text('Xem tiếp Tập 1'));
      await tester.pumpAndSettle();

      expect(find.text('EMBED https://v2.streamvsmov.com/video/1'), findsOneWidget);
    });

    testWidgets('Khách xem phim không ghi lịch sử', (tester) async {
      final deps = await pumpApp(tester, movies);
      await openMovie(tester, 'La chimera');
      await tester.tap(find.text('Xem phim'));
      await tester.pumpAndSettle();

      expect(deps.library.savedHistory, isEmpty);
    });

    testWidgets('Tab Lịch sử: vuốt để xoá, Xoá tất cả', (tester) async {
      final library = FakeLibraryRepository()
        ..history.addAll([
          FakeLibraryRepository.historyEntry(sampleSeries.slug, episodeSlug: 'tap-3', episodeName: '3'),
          FakeLibraryRepository.historyEntry(sampleSingle.slug, episodeSlug: 'tap-full', episodeName: 'Full'),
        ]);
      await pumpApp(tester, movies, signedIn: true, library: library);
      await openTab(tester, 'Thư viện');
      await tester.tap(find.text('Lịch sử'));
      await tester.pumpAndSettle();

      expect(find.byType(HistoryList), findsOneWidget);
      expect(find.textContaining('Tập 3 • Vietsub #1'), findsOneWidget);

      await tester.drag(find.text('La chimera').last, const Offset(-600, 0));
      await tester.pumpAndSettle();
      expect(library.history.map((e) => e.movie.slug), [sampleSeries.slug]);

      await tester.tap(find.text('Xoá tất cả'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Xoá'));
      await tester.pumpAndSettle();
      expect(library.history, isEmpty);
      expect(find.textContaining('Chưa xem phim nào'), findsOneWidget);
    });
  });

  test('timeAgo', () {
    final now = DateTime(2026, 10, 27, 12);
    expect(timeAgo(now.subtract(const Duration(seconds: 20)), now: now), 'Vừa xong');
    expect(timeAgo(now.subtract(const Duration(minutes: 5)), now: now), '5 phút trước');
    expect(timeAgo(now.subtract(const Duration(hours: 3)), now: now), '3 giờ trước');
    expect(timeAgo(now.subtract(const Duration(days: 2)), now: now), '2 ngày trước');
    expect(timeAgo(DateTime(2026, 9, 1), now: now), '01/09/2026');
  });
}
