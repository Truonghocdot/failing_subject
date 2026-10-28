import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/app/app.dart';
import 'package:mobile/core/api_client.dart';
import 'package:mobile/core/preferences.dart';
import 'package:mobile/core/token_storage.dart';
import 'package:mobile/data/auth_repository.dart';
import 'package:mobile/data/library_repository.dart';
import 'package:mobile/data/models/app_user.dart';
import 'package:mobile/data/models/home_section.dart';
import 'package:mobile/data/models/watch_history_entry.dart';
import 'package:mobile/data/models/movie_detail.dart';
import 'package:mobile/data/models/movie_summary.dart';
import 'package:mobile/data/models/paged_result.dart';
import 'package:mobile/data/models/taxonomy.dart';
import 'package:mobile/data/movie_repository.dart';
import 'package:mobile/features/player/embed_player.dart';
import 'package:shared_preferences/shared_preferences.dart';

// URL ảnh để trống → AppNetworkImage hiện ảnh dự phòng, không gọi mạng trong test.
MovieSummary summary(String slug, String name, {int? year = 2026, double? rating}) =>
    MovieSummary(slug: slug, name: name, posterUrl: '', thumbUrl: '', year: year, rating: rating);

final sampleSections = [
  HomeSection(
    key: 'phim-moi-cap-nhat',
    title: 'Mới cập nhật',
    items: [summary('mua-he-dang-nho-2026', 'Mùa Hè Đáng Nhớ'), summary('so-huu', 'Sở Hữu', rating: 7.5)],
  ),
  HomeSection(key: 'phim-le', title: 'Phim lẻ', items: [summary('chimera', 'La chimera', year: 2023)]),
];

const sampleSeries = MovieDetail(
  slug: 'mua-he-dang-nho-2026',
  name: 'Mùa Hè Đáng Nhớ',
  originName: 'Summer Vacation',
  content: 'Một cô gái tuổi teen bình thường khao khát điều mới mẻ.',
  type: 'series',
  status: 'ongoing',
  posterUrl: '',
  thumbUrl: '',
  episodeCurrent: 'Tập 3',
  quality: 'HD',
  lang: 'Vietsub',
  year: 2026,
  directors: ['Song Ji-won'],
  actors: ['Cho Yi-hyun', 'Jung Jin-young'],
  genres: [Taxonomy(id: 15, name: 'Chính Kịch', slug: 'chinh-kich')],
  countries: [Taxonomy(id: 10, name: 'Hàn Quốc', slug: 'han-quoc')],
  servers: [
    Server(
      name: 'Vietsub #1',
      episodes: [
        Episode(name: '1', slug: 'tap-1', embedUrl: 'https://v1.streamvsmov.com/video/1'),
        Episode(name: '2', slug: 'tap-2', embedUrl: 'https://v1.streamvsmov.com/video/2'),
        Episode(name: '3', slug: 'tap-3', embedUrl: 'https://v1.streamvsmov.com/video/3'),
      ],
    ),
    Server(
      name: 'Thuyết minh #1',
      episodes: [Episode(name: '1', slug: 'tap-1', embedUrl: 'https://v2.streamvsmov.com/video/1')],
    ),
  ],
);

const sampleSingle = MovieDetail(
  slug: 'chimera',
  name: 'La chimera',
  content: '',
  type: 'single',
  status: 'completed',
  posterUrl: '',
  thumbUrl: '',
  servers: [
    Server(
      name: 'Vietsub #1',
      episodes: [Episode(name: 'Full', slug: 'tap-full', embedUrl: 'https://v14.streamvsmov.com/video/x')],
    ),
  ],
);

class FakeMovieRepository extends Fake implements MovieRepository {
  FakeMovieRepository({this.sections, this.details = const {}, this.error});

  List<HomeSection>? sections;
  final Map<String, MovieDetail> details;

  /// Khác null → mọi lời gọi ném lỗi này.
  AppException? error;
  int homeCalls = 0;

  @override
  Future<List<HomeSection>> getHome() async {
    homeCalls++;
    if (error != null) throw error!;
    return sections ?? sampleSections;
  }

  @override
  Future<MovieDetail> getDetail(String slug) async {
    if (error != null) throw error!;
    return details[slug] ?? (throw const AppException('Không tìm thấy nội dung', statusCode: 404));
  }

  /// Ghi lại mọi truy vấn danh sách: "list:phim-bo:1", "search:mua:1", ...
  final List<String> pagedCalls = [];

  /// Số trang giả cho mọi danh sách (mỗi trang 3 phim).
  int totalPages = 2;

  PagedResult<MovieSummary> _page(String key, int page) {
    pagedCalls.add('$key:$page');
    if (error != null) throw error!;
    return PagedResult(
      items: [for (var i = 1; i <= 3; i++) summary('$key-$page-$i', 'Phim $key $page.$i')],
      page: page,
      pageSize: 3,
      totalItems: totalPages * 3,
      totalPages: totalPages,
    );
  }

  @override
  Future<PagedResult<MovieSummary>> getList(String list, {int page = 1}) async => _page('list-$list', page);

  @override
  Future<PagedResult<MovieSummary>> getByGenre(String slug, {int page = 1}) async => _page('genre-$slug', page);

  @override
  Future<PagedResult<MovieSummary>> getByCountry(String slug, {int page = 1}) async => _page('country-$slug', page);

  @override
  Future<PagedResult<MovieSummary>> getByYear(int year, {int page = 1}) async => _page('year-$year', page);

  @override
  Future<PagedResult<MovieSummary>> search(String keyword, {int page = 1}) async {
    pagedCalls.add('search-$keyword:$page');
    final matches = keyword.toLowerCase().contains('mua')
        ? [summary('mua-he-dang-nho-2026', 'Mùa Hè Đáng Nhớ')]
        : <MovieSummary>[];
    return PagedResult(
      items: matches,
      page: 1,
      pageSize: 24,
      totalItems: matches.length,
      totalPages: matches.isEmpty ? 0 : 1,
    );
  }

  @override
  Future<List<Taxonomy>> getGenres() async => const [
    Taxonomy(id: 15, name: 'Chính Kịch', slug: 'chinh-kich'),
    Taxonomy(id: 33, name: 'Lãng Mạn', slug: 'lang-man'),
  ];

  @override
  Future<List<Taxonomy>> getCountries() async => const [Taxonomy(id: 10, name: 'Hàn Quốc', slug: 'han-quoc')];
}

/// Thay WebView bằng Text để test (WebView cần platform thật).
Widget fakeEmbedPlayer(BuildContext context, String url) => Center(child: Text('EMBED $url'));

const sampleUser = AppUser(id: 'u-1', email: 'an@vsmovie.vn', displayName: 'An Nguyễn');
const samplePassword = 'matkhau123';

/// Backend auth giả: đúng [samplePassword] thì đăng nhập được; `me()` theo token đang lưu.
class FakeAuthRepository extends Fake implements AuthRepository {
  FakeAuthRepository(this.storage);

  final TokenStorage storage;
  final List<String> loggedOutTokens = [];
  final Set<String> registeredEmails = {sampleUser.email};

  AuthSession _session(AppUser user) => AuthSession(
    tokens: const AuthTokens(accessToken: 'access-1', refreshToken: 'refresh-1'),
    user: user,
  );

  @override
  Future<AuthSession> login({required String email, required String password}) async {
    if (email != sampleUser.email || password != samplePassword) {
      throw const AppException('Email hoặc mật khẩu không đúng', statusCode: 401);
    }
    return _session(sampleUser);
  }

  @override
  Future<AuthSession> register({required String email, required String password, required String displayName}) async {
    if (!registeredEmails.add(email)) throw const AppException('Email đã được sử dụng', statusCode: 409);
    return _session(AppUser(id: 'u-new', email: email, displayName: displayName));
  }

  @override
  Future<AppUser> me() async {
    if (await storage.read() == null) throw const AppException('Hết phiên', statusCode: 401);
    return sampleUser;
  }

  @override
  Future<void> logout(String refreshToken) async => loggedOutTokens.add(refreshToken);
}

/// /api/me/* giả, lưu trong bộ nhớ.
class FakeLibraryRepository extends Fake implements LibraryRepository {
  final List<MovieSummary> favorites = [];
  final List<WatchHistoryEntry> history = [];
  final List<String> savedHistory = [];

  @override
  Future<PagedResult<MovieSummary>> getFavorites({int page = 1}) async =>
      PagedResult(items: List.of(favorites), page: 1, pageSize: 24, totalItems: favorites.length, totalPages: 1);

  @override
  Future<bool> isFavorite(String slug) async => favorites.any((m) => m.slug == slug);

  @override
  Future<void> addFavorite(String slug) async => favorites.insert(0, summary(slug, _nameOf(slug)));

  @override
  Future<void> removeFavorite(String slug) async => favorites.removeWhere((m) => m.slug == slug);

  @override
  Future<PagedResult<WatchHistoryEntry>> getHistory({int page = 1}) async =>
      PagedResult(items: List.of(history), page: 1, pageSize: 24, totalItems: history.length, totalPages: 1);

  @override
  Future<WatchHistoryEntry?> getHistoryEntry(String slug) async =>
      history.where((e) => e.movie.slug == slug).firstOrNull;

  @override
  Future<void> saveHistory(
    String slug, {
    required String serverName,
    required String episodeSlug,
    required String episodeName,
  }) async {
    savedHistory.add('$slug/$serverName/$episodeSlug');
    history
      ..removeWhere((e) => e.movie.slug == slug)
      ..insert(0, historyEntry(slug, serverName: serverName, episodeSlug: episodeSlug, episodeName: episodeName));
  }

  @override
  Future<void> removeHistory(String slug) async => history.removeWhere((e) => e.movie.slug == slug);

  @override
  Future<void> clearHistory() async => history.clear();

  static String _nameOf(String slug) => switch (slug) {
    'mua-he-dang-nho-2026' => 'Mùa Hè Đáng Nhớ',
    'chimera' => 'La chimera',
    _ => slug,
  };

  static WatchHistoryEntry historyEntry(
    String slug, {
    String serverName = 'Vietsub #1',
    String episodeSlug = 'tap-1',
    String episodeName = '1',
  }) => WatchHistoryEntry(
    movie: summary(slug, _nameOf(slug)),
    serverName: serverName,
    episodeSlug: episodeSlug,
    episodeName: episodeName,
    watchedAt: DateTime.now(),
  );
}

/// Các phụ thuộc giả của một lần bơm app – test dùng để đặt dữ liệu và kiểm tra lời gọi.
class TestDeps {
  TestDeps({required this.storage, required this.auth, required this.library});

  final InMemoryTokenStorage storage;
  final FakeAuthRepository auth;
  final FakeLibraryRepository library;
}

/// Bơm toàn bộ app với repository giả, màn hình cao để mọi sliver đều được dựng.
/// [signedIn] = true → đã có token, mở app là đăng nhập sẵn [sampleUser].
Future<TestDeps> pumpApp(
  WidgetTester tester,
  FakeMovieRepository repository, {
  Map<String, Object> prefs = const {},
  bool signedIn = false,
  FakeLibraryRepository? library,
}) async {
  tester.view.physicalSize = const Size(1080, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);

  SharedPreferences.setMockInitialValues(prefs);
  final sharedPreferences = await SharedPreferences.getInstance();
  final storage = InMemoryTokenStorage(
    signedIn ? const AuthTokens(accessToken: 'access-0', refreshToken: 'refresh-0') : null,
  );
  final deps = TestDeps(
    storage: storage,
    auth: FakeAuthRepository(storage),
    library: library ?? FakeLibraryRepository(),
  );

  await tester.pumpWidget(
    ProviderScope(
      retry: (_, _) => null,
      overrides: [
        movieRepositoryProvider.overrideWithValue(repository),
        sharedPreferencesProvider.overrideWithValue(sharedPreferences),
        embedPlayerBuilderProvider.overrideWithValue(fakeEmbedPlayer),
        tokenStorageProvider.overrideWithValue(storage),
        authRepositoryProvider.overrideWithValue(deps.auth),
        libraryRepositoryProvider.overrideWithValue(deps.library),
      ],
      child: const VsMovieApp(),
    ),
  );
  await tester.pumpAndSettle();
  return deps;
}
