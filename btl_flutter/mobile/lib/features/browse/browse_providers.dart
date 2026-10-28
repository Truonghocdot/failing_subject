import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/taxonomy.dart';
import '../../data/movie_repository.dart';

// Danh mục ít thay đổi → giữ suốt phiên (không autoDispose).
final genresProvider = FutureProvider<List<Taxonomy>>((ref) => ref.watch(movieRepositoryProvider).getGenres());

final countriesProvider = FutureProvider<List<Taxonomy>>((ref) => ref.watch(movieRepositoryProvider).getCountries());

/// Quốc gia hay xem – hiện sẵn, không cần chờ API (slug đã đối chiếu với /api/countries).
const popularCountries = [
  Taxonomy(id: 0, name: 'Hàn Quốc', slug: 'han-quoc'),
  Taxonomy(id: 0, name: 'Trung Quốc', slug: 'trung-quoc'),
  Taxonomy(id: 0, name: 'Âu Mỹ', slug: 'au-my'),
  Taxonomy(id: 0, name: 'Nhật Bản', slug: 'nhat-ban'),
  Taxonomy(id: 0, name: 'Việt Nam', slug: 'viet-nam'),
  Taxonomy(id: 0, name: 'Thái Lan', slug: 'thai-lan'),
  Taxonomy(id: 0, name: 'Đài Loan', slug: 'dai-loan'),
  Taxonomy(id: 0, name: 'Hồng Kông', slug: 'hong-kong'),
  Taxonomy(id: 0, name: 'Ấn Độ', slug: 'an-do'),
  Taxonomy(id: 0, name: 'Mỹ', slug: 'my'),
  Taxonomy(id: 0, name: 'Anh', slug: 'anh'),
  Taxonomy(id: 0, name: 'Pháp', slug: 'phap'),
];
