import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/widgets/error_view.dart';
import '../data/movie_query.dart';
import '../features/auth/login_screen.dart';
import '../features/auth/register_screen.dart';
import '../features/browse/browse_screen.dart';
import '../features/browse/movie_list_screen.dart';
import '../features/detail/detail_screen.dart';
import '../features/home/home_screen.dart';
import '../features/library/library_screen.dart';
import '../features/player/player_screen.dart';
import '../features/search/search_screen.dart';
import 'app_shell.dart';

/// Đường dẫn route – dùng chung để tránh gõ chuỗi rời rạc.
abstract final class Routes {
  static const home = '/';
  static const browse = '/browse';
  static const search = '/search';
  static const library = '/library';
  static const login = '/login';
  static const register = '/register';

  static String movie(String slug) => '/movie/${Uri.encodeComponent(slug)}';

  /// Lưới phim đã lọc, VD `/list/genre/lang-man?title=Lãng Mạn`.
  static String list(MovieQuery query, {String? title}) => Uri(
    path: '/list/${query.kind.name}/${Uri.encodeComponent(query.value)}',
    queryParameters: title == null ? null : {'title': title},
  ).toString();

  /// Trình phát, VD `/watch/so-huu?server=0&ep=tap-3`.
  static String watch(String slug, {required int server, required String episode}) => Uri(
    path: '/watch/${Uri.encodeComponent(slug)}',
    queryParameters: {'server': '$server', 'ep': episode},
  ).toString();
}

final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: Routes.home,
    routes: [
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) => AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [GoRoute(path: Routes.home, builder: (context, state) => const HomeScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: Routes.browse, builder: (context, state) => const BrowseScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: Routes.search, builder: (context, state) => const SearchScreen())],
          ),
          StatefulShellBranch(
            routes: [GoRoute(path: Routes.library, builder: (context, state) => const LibraryScreen())],
          ),
        ],
      ),
      // Các route dưới đây nằm ngoài shell → mở toàn màn hình, che bottom navigation
      GoRoute(
        path: '/movie/:slug',
        builder: (context, state) => DetailScreen(slug: state.pathParameters['slug']!),
      ),
      GoRoute(
        path: '/list/:kind/:value',
        builder: (context, state) {
          final query = MovieQuery.tryParse(state.pathParameters['kind']!, state.pathParameters['value']!);
          return query == null
              ? Scaffold(
                  appBar: AppBar(),
                  body: const ErrorView(message: 'Đường dẫn không hợp lệ'),
                )
              : MovieListScreen(query: query, title: state.uri.queryParameters['title']);
        },
      ),
      GoRoute(
        path: '/watch/:slug',
        builder: (context, state) => PlayerScreen(
          slug: state.pathParameters['slug']!,
          serverIndex: int.tryParse(state.uri.queryParameters['server'] ?? '') ?? 0,
          episodeSlug: state.uri.queryParameters['ep'] ?? '',
        ),
      ),
      GoRoute(path: Routes.login, builder: (context, state) => const LoginScreen()),
      GoRoute(path: Routes.register, builder: (context, state) => const RegisterScreen()),
    ],
  );
});
