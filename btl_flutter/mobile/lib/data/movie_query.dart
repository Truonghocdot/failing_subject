import 'package:flutter/foundation.dart';

enum MovieQueryKind { list, genre, country, year, search, favorites }

/// Danh sách của vsmov mà backend hỗ trợ (`/api/movies?list=`).
const movieLists = <String, String>{
  'phim-moi-cap-nhat': 'Mới cập nhật',
  'phim-bo': 'Phim bộ',
  'phim-le': 'Phim lẻ',
  'phim-chieu-rap': 'Phim chiếu rạp',
};

/// Một truy vấn danh sách phim có phân trang – dùng làm key cho provider và route `/list/:kind/:value`.
@immutable
class MovieQuery {
  const MovieQuery(this.kind, this.value);

  const MovieQuery.list(String slug) : this(MovieQueryKind.list, slug);
  const MovieQuery.genre(String slug) : this(MovieQueryKind.genre, slug);
  const MovieQuery.country(String slug) : this(MovieQueryKind.country, slug);
  factory MovieQuery.year(int year) => MovieQuery(MovieQueryKind.year, '$year');
  const MovieQuery.search(String keyword) : this(MovieQueryKind.search, keyword);

  /// Phim yêu thích của người dùng đang đăng nhập.
  static const favorites = MovieQuery(MovieQueryKind.favorites, 'me');

  final MovieQueryKind kind;
  final String value;

  /// Từ tham số route; sai định dạng → null.
  static MovieQuery? tryParse(String kind, String value) {
    final k = MovieQueryKind.values.asNameMap()[kind];
    if (k == null || value.isEmpty) return null;
    if (k == MovieQueryKind.year && int.tryParse(value) == null) return null;
    return MovieQuery(k, value);
  }

  /// Tiêu đề mặc định khi route không truyền `title`.
  String get defaultTitle => switch (kind) {
    MovieQueryKind.list => movieLists[value] ?? 'Danh sách phim',
    MovieQueryKind.year => 'Phim năm $value',
    MovieQueryKind.search => 'Kết quả cho "$value"',
    MovieQueryKind.favorites => 'Phim yêu thích',
    MovieQueryKind.genre || MovieQueryKind.country => value,
  };

  @override
  bool operator ==(Object other) => other is MovieQuery && other.kind == kind && other.value == value;

  @override
  int get hashCode => Object.hash(kind, value);

  @override
  String toString() => 'MovieQuery(${kind.name}: $value)';
}
