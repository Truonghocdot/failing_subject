import 'package:flutter/material.dart';

import '../../core/widgets/coming_soon_view.dart';

class SearchScreen extends StatelessWidget {
  const SearchScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Tìm kiếm')),
      body: const ComingSoonView(
        icon: Icons.search,
        message: 'Tìm phim theo tên – Phase 4',
      ),
    );
  }
}
