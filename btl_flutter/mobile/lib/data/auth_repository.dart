import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../core/api_client.dart';
import 'models/app_user.dart';

final authRepositoryProvider = Provider<AuthRepository>((ref) => AuthRepository(ref.watch(apiClientProvider)));

/// /api/auth/* và /api/me (docs/06-database.md §6).
class AuthRepository {
  AuthRepository(this._api);

  static const _deviceName = 'VsMovie app';

  final ApiClient _api;

  Future<AuthSession> register({required String email, required String password, required String displayName}) =>
      _api.post(
        '/api/auth/register',
        auth: false,
        data: {'email': email, 'password': password, 'displayName': displayName, 'deviceName': _deviceName},
        parse: (json) => AuthSession.fromJson(json as Json),
      );

  Future<AuthSession> login({required String email, required String password}) => _api.post(
    '/api/auth/login',
    auth: false,
    data: {'email': email, 'password': password, 'deviceName': _deviceName},
    parse: (json) => AuthSession.fromJson(json as Json),
  );

  Future<void> logout(String refreshToken) =>
      _api.post('/api/auth/logout', auth: false, data: {'refreshToken': refreshToken}, parse: (_) {});

  Future<AppUser> me() => _api.get('/api/me', parse: (json) => AppUser.fromJson(json as Json));
}
