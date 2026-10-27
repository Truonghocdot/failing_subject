import 'package:flutter/material.dart';

import '../../core/widgets/coming_soon_view.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('VsMovie')),
      body: const ComingSoonView(
        icon: Icons.movie_outlined,
        message: 'Banner và các hàng phim mới – Phase 2',
      ),
    );
  }
}
