import 'package:flutter/material.dart';

import '../../data/movie_query.dart';
import 'widgets/movie_grid.dart';

/// Lưới phim đã lọc: danh sách / thể loại / quốc gia / năm (route `/list/:kind/:value`).
class MovieListScreen extends StatelessWidget {
  const MovieListScreen({super.key, required this.query, this.title});

  final MovieQuery query;
  final String? title;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title ?? query.defaultTitle)),
      body: MovieGrid(query: query),
    );
  }
}
