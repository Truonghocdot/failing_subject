import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/browse/browse_screen.dart';
import '../features/home/home_screen.dart';
import '../features/library/library_screen.dart';
import '../features/search/search_screen.dart';
import 'app_shell.dart';

/// Đường dẫn route – dùng chung để tránh gõ chuỗi rời rạc.
abstract final class Routes {
  static const home = '/';
  static const browse = '/browse';
  static const search = '/search';
  static const library = '/library';
}

final routerProvider = Provider<GoRouter>((ref) {
  return GoRouter(
    initialLocation: Routes.home,
    routes: [
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            AppShell(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: Routes.home, builder: (context, state) => const HomeScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: Routes.browse, builder: (context, state) => const BrowseScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: Routes.search, builder: (context, state) => const SearchScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: Routes.library, builder: (context, state) => const LibraryScreen()),
          ]),
        ],
      ),
      // Phase 2+: /movie/:slug, /watch/:slug/:episode, /login, /register (ngoài shell)
    ],
  );
});
