import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../app/router.dart';
import '../../../core/api_client.dart';
import '../../auth/auth_providers.dart';
import '../library_providers.dart';

/// Nút ♥ trên trang chi tiết. Khách bấm → mở đăng nhập.
class FavoriteButton extends ConsumerWidget {
  const FavoriteButton({super.key, required this.slug});

  final String slug;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (ref.watch(currentUserIdProvider) == null) {
      return IconButton(
        tooltip: 'Đăng nhập để lưu phim yêu thích',
        onPressed: () => context.push(Routes.login),
        icon: const Icon(Icons.favorite_border),
      );
    }

    final status = ref.watch(favoriteStatusProvider(slug));
    final isFavorite = status.value ?? false;
    return IconButton(
      tooltip: isFavorite ? 'Bỏ yêu thích' : 'Thêm vào yêu thích',
      onPressed: status.hasValue
          ? () async {
              try {
                await ref.read(favoriteStatusProvider(slug).notifier).toggle();
              } catch (e) {
                if (context.mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(errorMessage(e))));
                }
              }
            }
          : null,
      icon: Icon(
        isFavorite ? Icons.favorite : Icons.favorite_border,
        color: isFavorite ? Theme.of(context).colorScheme.primary : null,
      ),
    );
  }
}
