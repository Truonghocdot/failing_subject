import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/home_section.dart';
import '../../data/movie_repository.dart';

final homeProvider = FutureProvider<List<HomeSection>>((ref) => ref.watch(movieRepositoryProvider).getHome());
