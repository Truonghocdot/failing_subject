import 'dart:convert';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import 'api_client.dart';

class AuthTokens {
  const AuthTokens({required this.accessToken, required this.refreshToken});

  /// Từ `AuthResponse` của backend hoặc từ chính [toJson].
  factory AuthTokens.fromJson(Json json) =>
      AuthTokens(accessToken: json['accessToken'] as String, refreshToken: json['refreshToken'] as String);

  final String accessToken;
  final String refreshToken;

  Json toJson() => {'accessToken': accessToken, 'refreshToken': refreshToken};
}

/// Nơi giữ token đăng nhập. Bản thật dùng Keystore/Keychain; test dùng [InMemoryTokenStorage].
abstract class TokenStorage {
  Future<AuthTokens?> read();
  Future<void> save(AuthTokens tokens);
  Future<void> clear();
}

final tokenStorageProvider = Provider<TokenStorage>((ref) => SecureTokenStorage());

class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage([this._storage = const FlutterSecureStorage()]);

  static const _key = 'auth_tokens';
  final FlutterSecureStorage _storage;

  // Đọc secure storage tốn vài ms → giữ bản trong bộ nhớ sau lần đọc đầu (mỗi request đều cần token)
  AuthTokens? _cached;
  bool _loaded = false;

  @override
  Future<AuthTokens?> read() async {
    if (_loaded) return _cached;
    final raw = await _storage.read(key: _key);
    _cached = raw == null ? null : AuthTokens.fromJson(jsonDecode(raw) as Json);
    _loaded = true;
    return _cached;
  }

  @override
  Future<void> save(AuthTokens tokens) async {
    _cached = tokens;
    _loaded = true;
    await _storage.write(key: _key, value: jsonEncode(tokens.toJson()));
  }

  @override
  Future<void> clear() async {
    _cached = null;
    _loaded = true;
    await _storage.delete(key: _key);
  }
}

class InMemoryTokenStorage implements TokenStorage {
  InMemoryTokenStorage([this.tokens]);

  AuthTokens? tokens;

  @override
  Future<AuthTokens?> read() async => tokens;

  @override
  Future<void> save(AuthTokens value) async => tokens = value;

  @override
  Future<void> clear() async => tokens = null;
}
