# 04 – Ứng dụng Flutter

## 1. Tổng quan

- Flutter 3.47 / Dart 3.13, Material 3, **giao diện tối** (phù hợp app xem phim).
- Nền tảng mục tiêu: **Android** (chính), iOS. Không ưu tiên Web/Desktop vì `webview_flutter` không hỗ trợ đầy đủ.
- Chỉ gọi **backend của mình**, không gọi thẳng vsmov (trừ WebView player).

## 2. Package

Đã thêm vào `pubspec.yaml`:

| Package | Dùng cho |
|---|---|
| `dio` | HTTP client, timeout, interceptor gắn token + tự refresh |
| `flutter_riverpod` | State management + DI (Provider/FutureProvider/Notifier) |
| `go_router` | Điều hướng, deep link `/movie/:slug`, redirect khi cần đăng nhập |
| `cached_network_image` | Cache ảnh poster/thumb |
| `webview_flutter` | Phát phim qua embed player |
| `flutter_secure_storage` | Lưu access/refresh token (Keystore/Keychain) |
| `shared_preferences` | Cài đặt nhẹ: từ khoá tìm kiếm gần đây, server ưa thích |

Không dùng code generation (freezed/json_serializable) để giữ dự án nhỏ – viết `fromJson` thủ công.

## 3. Cấu trúc thư mục

```
mobile/lib/
├── main.dart                      # ProviderScope + MaterialApp.router
├── app/
│   ├── router.dart                # go_router: routes + ShellRoute bottom nav
│   ├── theme.dart                 # dark theme
│   └── env.dart                   # API_BASE_URL từ --dart-define
├── core/
│   ├── api_client.dart            # Dio instance + map lỗi → AppException + AuthInterceptor
│   ├── token_storage.dart         # flutter_secure_storage
│   └── widgets/                   # MovieCard, MovieGrid, SectionRow, ErrorView, LoadingView
├── data/
│   ├── models/                    # movie_summary, movie_detail, paged_result, taxonomy, user, watch_history
│   ├── movie_repository.dart      # gọi /api/movies, /api/home, ...
│   ├── auth_repository.dart       # /api/auth/*, /api/me
│   └── library_repository.dart    # /api/me/favorites, /api/me/history
└── features/
    ├── auth/        login_screen.dart, register_screen.dart, auth_providers.dart
    ├── home/        home_screen.dart, home_providers.dart
    ├── browse/      browse_screen.dart (lưới + infinite scroll, dùng chung cho list/genre/country/year)
    ├── search/      search_screen.dart (debounce 400ms)
    ├── detail/      detail_screen.dart, detail_providers.dart
    ├── player/      player_screen.dart
    └── library/     library_screen.dart (tab Yêu thích | Lịch sử)
```

## 4. Màn hình & điều hướng

```
BottomNav: [Trang chủ] [Khám phá] [Tìm kiếm] [Thư viện]

/                       HomeScreen
/browse                 BrowseScreen (chip thể loại / quốc gia / năm)
/browse/:kind/:value    BrowseScreen đã lọc  (kind = list|genre|country|year)
/search                 SearchScreen
/library                LibraryScreen         (chưa đăng nhập → màn hình mời đăng nhập)
/login, /register       Đăng nhập / Đăng ký   (ngoài shell)
/movie/:slug            DetailScreen          (ngoài shell – full màn hình)
/watch/:slug/:episode   PlayerScreen          (extra: embedUrl, serverIndex)
```

| Màn hình | Nội dung | API |
|---|---|---|
| Home | Banner (carousel 5 phim mới), các hàng ngang: Mới cập nhật, Phim bộ, Phim lẻ, Chiếu rạp. Nút "Xem tất cả" → Browse | `GET /api/home` |
| Browse | Lưới 3 cột, infinite scroll, pull-to-refresh | `/api/movies?list=`, `/api/genres/{s}/movies`, ... |
| Search | Ô tìm kiếm, debounce, kết quả dạng lưới, trạng thái rỗng | `/api/movies/search` |
| Detail | Ảnh thumb nền + poster, tên, tên gốc, chip (năm, chất lượng, ngôn ngữ, tập hiện tại), mô tả (thu gọn/mở rộng), thể loại (bấm → Browse), diễn viên, **tab server + lưới tập**, nút ♥ yêu thích, nút "Xem tiếp tập X" nếu có lịch sử | `/api/movies/{slug}` |
| Player | WebView toàn màn hình, xoay ngang, nút tập trước/sau, chọn server | – (embedUrl) |
| Library | Thông tin tài khoản + Đăng xuất; tab Yêu thích, tab Lịch sử (mới nhất trước, vuốt để xoá) | `/api/me/*` |
| Login / Register | Form email, mật khẩu (≥ 8 ký tự), tên hiển thị; validate tại chỗ | `/api/auth/*` |

## 5. Player (WebView)

```dart
final controller = WebViewController()
  ..setJavaScriptMode(JavaScriptMode.unrestricted)
  ..setBackgroundColor(Colors.black)
  ..setNavigationDelegate(NavigationDelegate(
    onNavigationRequest: (req) {
      final host = Uri.tryParse(req.url)?.host ?? '';
      // chặn quảng cáo/popup điều hướng ra ngoài domain player
      return host.endsWith('streamvsmov.com')
          ? NavigationDecision.navigate
          : NavigationDecision.prevent;
    },
  ))
  ..loadRequest(Uri.parse(embedUrl));
```

- Khi vào Player: `SystemChrome.setPreferredOrientations([landscapeLeft, landscapeRight])` + `SystemUiMode.immersiveSticky`; khi thoát khôi phục lại.
- Android: bật fullscreen video trong WebView (`AndroidWebViewController` – `setMediaPlaybackRequiresUserGesture(false)`).
- Nếu đã đăng nhập: ghi lịch sử ngay khi mở tập (`PUT /api/me/history/{slug}`). Không lấy được vị trí phát từ embed player → chỉ lưu **đến tập nào**.

## 6. State (Riverpod)

```dart
final apiClientProvider       = Provider((ref) => ApiClient(Env.apiBaseUrl));
final movieRepositoryProvider = Provider((ref) => MovieRepository(ref.watch(apiClientProvider)));

final homeProvider        = FutureProvider((ref) => ref.watch(movieRepositoryProvider).getHome());
final movieDetailProvider = FutureProvider.family<MovieDetail, String>(
    (ref, slug) => ref.watch(movieRepositoryProvider).getDetail(slug));

final authProvider      = NotifierProvider<AuthNotifier, AuthState>(AuthNotifier.new);   // guest | loggedIn(user)
final isFavoriteProvider = FutureProvider.family<bool, String>(...);                       // GET /api/me/favorites/{slug}
final favoritesProvider = /* PagedNotifier */;  final historyProvider = /* PagedNotifier */;
```

Infinite scroll: `PagedNotifier` giữ `items`, `page`, `hasMore`, `isLoadingMore`; tải trang kế khi cuộn còn cách đáy ~300px.

## 7. Lưu trữ local

| Nơi lưu | Key | Giá trị |
|---|---|---|
| `flutter_secure_storage` | `access_token`, `refresh_token` | Token đăng nhập |
| `shared_preferences` | `recent_searches` | Tối đa 10 từ khoá gần đây |

Yêu thích và lịch sử lưu trên server ([06-database.md](06-database.md)). Khách bấm ♥ → chuyển sang `/login`.

## 8. Cấu hình môi trường

Cấu hình nằm ở `mobile/.env` (mẫu: `mobile/.env.example`), nạp bằng tham số có sẵn của Flutter:

```bash
flutter run --dart-define-from-file=.env
```

```dart
// app/env.dart
class Env {
  static const apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5080');
  static const apiTimeout = int.fromEnvironment('API_TIMEOUT_SECONDS', defaultValue: 15);
  static const playerHost = String.fromEnvironment('PLAYER_ALLOWED_HOST_SUFFIX', defaultValue: 'streamvsmov.com');
}
```

Giá trị trong `.env` bị nhúng vào APK → **không đặt secret**.

| Môi trường | `API_BASE_URL` |
|---|---|
| Android emulator | `http://10.0.2.2:5080` |
| iOS simulator | `http://localhost:5080` |
| Máy thật (cùng Wi-Fi) | `http://<IP-máy-tính>:5080` |

Android – bắt buộc:
- `android/app/src/main/AndroidManifest.xml`: `<uses-permission android:name="android.permission.INTERNET"/>` (bản release).
- Gọi backend `http://` khi dev: thêm `android:usesCleartextTraffic="true"` vào `<application>` (hoặc `network_security_config` chỉ cho IP dev).
- `minSdk` ≥ 21 (yêu cầu của `webview_flutter`).

## 9. Hiệu năng ảnh

- Danh sách dùng `thumbUrl`/`posterUrl` với `CachedNetworkImage(memCacheWidth: 300)` – ảnh gốc ~1.5 MB.
- Placeholder: khung xám bo góc (shimmer đơn giản), lỗi ảnh → icon `movie`.
