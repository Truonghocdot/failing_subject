import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/token_storage.dart';
import '../../data/auth_repository.dart';
import '../../data/models/app_user.dart';

/// Người dùng hiện tại; `null` = khách. Khi mở app: có token → gọi /api/me để khôi phục phiên.
class AuthNotifier extends AsyncNotifier<AppUser?> {
  TokenStorage get _storage => ref.read(tokenStorageProvider);
  AuthRepository get _repository => ref.read(authRepositoryProvider);

  @override
  Future<AppUser?> build() async {
    final subscription = ref.watch(sessionExpiredEventsProvider).stream.listen((_) => _onSessionExpired());
    ref.onDispose(subscription.cancel);

    if (await ref.watch(tokenStorageProvider).read() == null) return null;
    try {
      return await _repository.me();
    } on AppException catch (e) {
      if (e.statusCode != 401) rethrow; // lỗi mạng → giữ token, hiện lỗi + Thử lại
      await _storage.clear();
      return null;
    }
  }

  Future<void> login({required String email, required String password}) =>
      _signIn(_repository.login(email: email.trim(), password: password));

  Future<void> register({required String email, required String password, required String displayName}) =>
      _signIn(_repository.register(email: email.trim(), password: password, displayName: displayName.trim()));

  Future<void> logout() async {
    final tokens = await _storage.read();
    await _storage.clear();
    state = const AsyncData(null);
    if (tokens != null) {
      try {
        await _repository.logout(tokens.refreshToken);
      } catch (_) {
        // thu hồi token phía server thất bại cũng không sao – token đã bị xoá khỏi máy
      }
    }
  }

  /// Lỗi (sai mật khẩu, email trùng...) được ném lại cho form hiển thị; state giữ nguyên.
  Future<void> _signIn(Future<AuthSession> request) async {
    final session = await request;
    await _storage.save(session.tokens);
    state = AsyncData(session.user);
  }

  void _onSessionExpired() {
    if (state.isLoading) return; // build() tự xử lý 401
    state = const AsyncData(null);
  }
}

final authProvider = AsyncNotifierProvider<AuthNotifier, AppUser?>(AuthNotifier.new);

/// Id người dùng hiện tại – các provider dữ liệu cá nhân `watch` cái này để tự tải lại khi đổi tài khoản.
final currentUserIdProvider = Provider<String?>((ref) => ref.watch(authProvider).value?.id);
