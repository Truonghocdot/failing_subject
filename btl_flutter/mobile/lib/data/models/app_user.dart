import '../../core/api_client.dart';
import '../../core/token_storage.dart';

/// Người dùng – khớp `UserDto` của backend.
class AppUser {
  const AppUser({required this.id, required this.email, required this.displayName, this.avatarUrl});

  factory AppUser.fromJson(Json json) => AppUser(
    id: json['id'] as String,
    email: json['email'] as String,
    displayName: json['displayName'] as String,
    avatarUrl: json['avatarUrl'] as String?,
  );

  final String id;
  final String email;
  final String displayName;
  final String? avatarUrl;

  String get initial => displayName.trim().isEmpty ? '?' : displayName.trim()[0].toUpperCase();
}

/// Kết quả đăng nhập/đăng ký – khớp `AuthResponse`.
class AuthSession {
  const AuthSession({required this.tokens, required this.user});

  factory AuthSession.fromJson(Json json) =>
      AuthSession(tokens: AuthTokens.fromJson(json), user: AppUser.fromJson(json['user'] as Json));

  final AuthTokens tokens;
  final AppUser user;
}
