import 'package:flutter/material.dart';

import '../../core/widgets/coming_soon_view.dart';

class BrowseScreen extends StatelessWidget {
  const BrowseScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Khám phá')),
      body: const ComingSoonView(
        icon: Icons.explore_outlined,
        message: 'Duyệt theo danh sách, thể loại, quốc gia, năm – Phase 4',
      ),
    );
  }
}
