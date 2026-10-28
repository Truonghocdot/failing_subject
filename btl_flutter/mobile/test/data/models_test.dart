import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/data/models/home_section.dart';
import 'package:mobile/data/models/movie_detail.dart';
import 'package:mobile/data/models/movie_summary.dart';
import 'package:mobile/data/models/paged_result.dart';

void main() {
  // JSON đúng định dạng backend trả (GET /api/movies/{slug})
  final detailJson = <String, dynamic>{
    'slug': 'mua-he-dang-nho-2026',
    'name': 'Mùa Hè Đáng Nhớ',
    'originName': 'Summer Vacation',
    'content': 'Nội dung',
    'type': 'series',
    'status': 'ongoing',
    'posterUrl': 'https://vsmov.com/p.jpg',
    'thumbUrl': 'https://vsmov.com/t.jpg',
    'trailerUrl': null,
    'duration': '60 phút',
    'episodeCurrent': 'Tập 1',
    'episodeTotal': '10',
    'quality': 'HD',
    'lang': 'Vietsub',
    'showtimes': null,
    'year': 2026,
    'rating': null,
    'actors': ['Cho Yi-hyun'],
    'directors': ['Song Ji-won'],
    'genres': [
      {'id': 15, 'name': 'Chính Kịch', 'slug': 'chinh-kich'},
    ],
    'countries': [
      {'id': 10, 'name': 'Hàn Quốc', 'slug': 'han-quoc'},
    ],
    'servers': [
      {
        'name': 'Vietsub #1',
        'episodes': [
          {'name': '1', 'slug': 'tap-1', 'embedUrl': 'https://v1.streamvsmov.com/video/a'},
        ],
      },
    ],
  };

  test('MovieDetail.fromJson đọc đủ trường', () {
    final movie = MovieDetail.fromJson(detailJson);

    expect(movie.name, 'Mùa Hè Đáng Nhớ');
    expect(movie.year, 2026);
    expect(movie.rating, isNull);
    expect(movie.genres.single.slug, 'chinh-kich');
    expect(movie.servers.single.episodes.single.embedUrl, 'https://v1.streamvsmov.com/video/a');
    expect(movie.showsEpisodeGrid, isTrue);
    expect(movie.statusLabel, 'Đang chiếu');
  });

  test('Phim lẻ 1 tập không hiện lưới tập', () {
    final movie = MovieDetail.fromJson({
      ...detailJson,
      'type': 'single',
      'servers': [
        {
          'name': 'Vietsub #1',
          'episodes': [
            {'name': 'Full', 'slug': 'tap-full', 'embedUrl': 'https://v1.streamvsmov.com/video/b'},
          ],
        },
      ],
    });

    expect(movie.showsEpisodeGrid, isFalse);
    expect(movie.hasEpisodes, isTrue);
  });

  test('Episode.label', () {
    expect(const Episode(name: '12', slug: 'tap-12', embedUrl: '').label, 'Tập 12');
    expect(const Episode(name: 'Full', slug: 'tap-full', embedUrl: '').label, 'Full');
  });

  test('PagedResult.hasMore', () {
    PagedResult<MovieSummary> page(int current, int total) => PagedResult.fromJson({
      'items': [
        {'slug': 'a', 'name': 'A', 'posterUrl': '', 'thumbUrl': '', 'year': 2024, 'rating': 7.3},
      ],
      'page': current,
      'pageSize': 24,
      'totalItems': 100,
      'totalPages': total,
    }, MovieSummary.fromJson);

    expect(page(1, 5).hasMore, isTrue);
    expect(page(5, 5).hasMore, isFalse);
    expect(page(1, 5).items.single.rating, 7.3);
  });

  test('HomeSection.fromJson', () {
    final section = HomeSection.fromJson({
      'key': 'phim-bo',
      'title': 'Phim bộ',
      'items': [
        {'slug': 'so-huu', 'name': 'Sở Hữu', 'originName': 'Mine', 'posterUrl': 'p', 'thumbUrl': 't', 'year': 2021},
      ],
    });

    expect(section.title, 'Phim bộ');
    expect(section.items.single.originName, 'Mine');
    expect(section.items.single.rating, isNull);
  });
}
