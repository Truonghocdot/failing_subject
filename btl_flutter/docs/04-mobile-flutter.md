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

/                          HomeScreen
/browse                    BrowseScreen (chip danh sách / thể loại / quốc gia / năm)
/search                    SearchScreen
/library                   LibraryScreen         (chưa đăng nhập → màn hình mời đăng nhập)

Ngoài shell (full màn hình, che bottom nav):
/movie/:slug               DetailScreen
/list/:kind/:value?title=  MovieListScreen – lưới phim đã lọc (kind = list|genre|country|year)
/watch/:slug?server=&ep=   PlayerScreen – VD /watch/so-huu?server=0&ep=tap-3
/login, /register          Đăng nhập / Đăng ký   (Phase 6)
```

Dùng `Routes.movie(slug)`, `Routes.list(MovieQuery, title:)`, `Routes.watch(slug, server:, episode:)` trong `lib/app/router.dart` thay vì gõ chuỗi. Player chỉ nhận `slug` + chỉ số server + slug tập rồi tự lấy `embedUrl` từ `movieDetailProvider` (đã có sẵn khi đi từ trang chi tiết) → mở lại bằng deep link vẫn chạy.

| Màn hình | Nội dung | API |
|---|---|---|
| Home | Banner (carousel 5 phim mới), các hàng ngang: Mới cập nhật, Phim bộ, Phim lẻ, Chiếu rạp. Nút "Xem tất cả" → `/list/list/{slug}` | `GET /api/home` |
| Browse (tab) | Chip danh sách, thể loại (API), 12 quốc gia phổ biến + "Tất cả quốc gia" (bottom sheet), 15 năm gần nhất | `/api/genres`, `/api/countries` |
| MovieList | `MovieGrid`: lưới ≥ 3 cột, infinite scroll, pull-to-refresh, lỗi tải thêm có nút "Tải tiếp" | `/api/movies?list=`, `/api/genres/{s}/movies`, ... |
| Search | Ô tìm kiếm, debounce 400ms, ≥ 2 ký tự, `MovieGrid`, trạng thái rỗng, từ khoá gần đây (lưu khi bấm Enter hoặc mở 1 kết quả) | `/api/movies/search` |
| Detail | Ảnh thumb nền + poster, tên, tên gốc, nhãn (năm, chất lượng, ngôn ngữ, tập hiện tại...), mô tả (thu gọn/mở rộng), chip thể loại + quốc gia (bấm → MovieList), đạo diễn, diễn viên, **chọn server + lưới tập**, nút ♥ yêu thích, nút "Xem tiếp tập X" nếu có lịch sử (Phase 6) | `/api/movies/{slug}` |
| Player | WebView toàn màn hình, xoay ngang, thanh điều khiển tự ẩn sau 4s (Quay lại, Tập trước/sau, Chọn tập + server qua bottom sheet) | – (embedUrl) |
| Library | Thông tin tài khoản + Đăng xuất; tab Yêu thích, tab Lịch sử (mới nhất trước, vuốt để xoá) | `/api/me/*` |
| Login / Register | Form email, mật khẩu (≥ 8 ký tự), tên hiển thị; validate tại chỗ | `/api/auth/*` |

## 5. Player (WebView)

Code: `lib/features/player/` – `player_screen.dart` (điều khiển, chọn tập), `embed_player.dart` (WebView), `episode_picker_sheet.dart`.

```dart
// embed_player.dart – chỉ chặn điều hướng KHUNG CHÍNH ra ngoài domain player; iframe/CDN/font không bị ảnh hưởng
bool isAllowedPlayerNavigation(String url, {required bool isMainFrame, String hostSuffix = Env.playerAllowedHostSuffix}) {
  if (!isMainFrame) return true;
  if (url == 'about:blank') return true;
  final uri = Uri.tryParse(url);
  if (uri == null || (uri.scheme != 'https' && uri.scheme != 'http')) return false;   // chặn intent://, market://
  return uri.host == hostSuffix || uri.host.endsWith('.$hostSuffix');                 // chặn evilstreamvsmov.com
}
```

- Khi vào Player: `SystemChrome.setPreferredOrientations([landscapeLeft, landscapeRight])` + `SystemUiMode.immersiveSticky`; khi thoát trả về mặc định + `edgeToEdge`.
- Android (`webview_flutter_android`): `setMediaPlaybackRequiresUserGesture(false)`; `setCustomWidgetCallbacks` để nút fullscreen của JW Player hoạt động, nút Back thoát fullscreen trước khi thoát màn hình.
- WebView được tạo qua `embedPlayerBuilderProvider` → test override bằng widget giả (WebView cần platform thật).
- Đã kiểm tra trang embed: không redirect, tài nguyên con từ `*.streamvsmov.com`, cdnjs, Google Fonts.
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

// Phase 4 – mọi lưới phim phân trang
final pagedMoviesProvider = AsyncNotifierProvider.autoDispose.family<PagedMoviesNotifier, PagedMovies, MovieQuery>(
  PagedMoviesNotifier.new);
final genresProvider = FutureProvider(...);  final countriesProvider = FutureProvider(...);   // giữ suốt phiên
final recentSearchesProvider = NotifierProvider<RecentSearchesNotifier, List<String>>(...);  // shared_preferences
```

Infinite scroll: `PagedMoviesNotifier` (`lib/features/browse/paged_movies_provider.dart`) – `build()` tải trang 1, `loadMore()` nối trang kế khi cuộn còn cách đáy 400px; bỏ phim trùng giữa các trang; trang rỗng → dừng; lỗi tải thêm giữ dữ liệu cũ + nút "Tải tiếp". `MovieQuery` (`lib/data/movie_query.dart`) là key: `list | genre | country | year | search` + value.

Riverpod 3 tự retry provider lỗi → đã tắt bằng `ProviderScope(retry: (_, _) => null)`; `SharedPreferences` nạp trong `main()` rồi override `sharedPreferencesProvider`.

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
