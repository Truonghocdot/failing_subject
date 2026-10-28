import 'package:dio/dio.dart';

import 'api_client.dart';
import 'token_storage.dart';

/// Gắn `Authorization: Bearer` và tự làm mới token khi gặp 401.
///
/// - [QueuedInterceptor] xử lý lỗi tuần tự → nhiều request 401 cùng lúc chỉ refresh một lần;
///   request đến sau thấy token đã đổi thì thử lại luôn với token mới.
/// - Refresh/thử lại dùng [plainDio] (không có interceptor) để tránh tự chờ chính hàng đợi của mình.
/// - Refresh bị từ chối → xoá token, gọi [onSessionExpired]. Lỗi mạng khi refresh → giữ phiên, trả lỗi gốc.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor({required this.storage, required this.plainDio, required this.onSessionExpired});

  static const skipAuthKey = 'skipAuth';
  static const _retriedKey = 'authRetried';

  final TokenStorage storage;
  final Dio plainDio;
  final void Function() onSessionExpired;

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    if (options.extra[skipAuthKey] != true) {
      final tokens = await storage.read();
      if (tokens != null) options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final options = err.requestOptions;
    final unauthorized = err.response?.statusCode == 401;
    if (!unauthorized || options.extra[skipAuthKey] == true || options.extra[_retriedKey] == true) {
      return handler.next(err);
    }

    final tokens = await storage.read();
    if (tokens == null) return handler.next(err);

    final AuthTokens? fresh;
    try {
      final sent = (options.headers['Authorization'] as String?)?.replaceFirst('Bearer ', '');
      fresh = sent == tokens.accessToken ? await _refresh(tokens.refreshToken) : tokens;
    } on DioException {
      return handler.next(err); // mất mạng khi refresh – không đăng xuất
    }

    if (fresh == null) {
      await storage.clear();
      onSessionExpired();
      return handler.next(err);
    }

    try {
      options
        ..headers['Authorization'] = 'Bearer ${fresh.accessToken}'
        ..extra[_retriedKey] = true;
      handler.resolve(await plainDio.fetch<Object?>(options));
    } on DioException catch (retryError) {
      handler.next(retryError);
    }
  }

  /// Token mới, hoặc null nếu backend từ chối refresh token (hết hạn/thu hồi).
  Future<AuthTokens?> _refresh(String refreshToken) async {
    try {
      final response = await plainDio.post<Object?>('/api/auth/refresh', data: {'refreshToken': refreshToken});
      final tokens = AuthTokens.fromJson(response.data! as Json);
      await storage.save(tokens);
      return tokens;
    } on DioException catch (e) {
      final status = e.response?.statusCode;
      if (status == 400 || status == 401) return null;
      rethrow;
    }
  }
}
