import 'package:flutter/material.dart';

import '../../core/widgets/coming_soon_view.dart';

class LibraryScreen extends StatelessWidget {
  const LibraryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Thư viện')),
      body: const ComingSoonView(
        icon: Icons.video_library_outlined,
        message: 'Tài khoản, phim yêu thích và lịch sử xem – Phase 6',
      ),
    );
  }
}
