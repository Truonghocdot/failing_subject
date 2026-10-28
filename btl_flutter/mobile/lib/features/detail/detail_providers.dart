import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/movie_detail.dart';
import '../../data/movie_repository.dart';

/// Tự huỷ khi rời màn hình chi tiết (backend đã cache 10 phút).
final movieDetailProvider = FutureProvider.autoDispose.family<MovieDetail, String>(
  (ref, slug) => ref.watch(movieRepositoryProvider).getDetail(slug),
);
