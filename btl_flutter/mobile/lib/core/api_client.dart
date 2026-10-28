import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../app/env.dart';
import 'auth_interceptor.dart';
import 'token_storage.dart';

typedef Json = Map<String, dynamic>;

/// Lỗi đã được dịch sang thông điệp tiếng Việt để hiển thị trực tiếp.
class AppException implements Exception {
  const AppException(this.message, {this.statusCode});

  final String message;
  final int? statusCode;

  @override
  String toString() => 'AppException($statusCode): $message';
}

/// Thông điệp hiển thị cho mọi lỗi (AppException hoặc lỗi bất ngờ).
String errorMessage(Object error) => error is AppException ? error.message : 'Đã có lỗi xảy ra, vui lòng thử lại';

/// Phát sự kiện khi phiên đăng nhập hết hạn (refresh token bị từ chối) – AuthNotifier lắng nghe.
final sessionExpiredEventsProvider = Provider<StreamController<void>>((ref) {
  final controller = StreamController<void>.broadcast();
  ref.onDispose(controller.close);
  return controller;
});

final dioProvider = Provider<Dio>((ref) {
  const timeout = Duration(seconds: Env.apiTimeoutSeconds);
  final options = BaseOptions(
    baseUrl: Env.apiBaseUrl,
    connectTimeout: timeout,
    receiveTimeout: timeout,
    responseType: ResponseType.json,
  );
  final dio = Dio(options);
  final events = ref.watch(sessionExpiredEventsProvider);
  dio.interceptors.add(
    AuthInterceptor(
      storage: ref.watch(tokenStorageProvider),
      plainDio: Dio(options),
      onSessionExpired: () => events.add(null),
    ),
  );
  if (kDebugMode) {
    dio.interceptors.add(LogInterceptor(requestHeader: false, responseHeader: false, responseBody: false));
  }
  return dio;
});

final apiClientProvider = Provider<ApiClient>((ref) => ApiClient(ref.watch(dioProvider)));

/// Bọc Dio: gọi backend, parse JSON, chuyển mọi lỗi thành [AppException].
/// [auth] = false cho API không cần token (đăng nhập, đăng ký...) – 401 khi đó không kích hoạt refresh.
class ApiClient {
  ApiClient(this._dio);

  final Dio _dio;

  Future<T> get<T>(
    String path, {
    Map<String, Object?>? query,
    required T Function(Object? json) parse,
    bool auth = true,
  }) => _send('GET', path, query: query, parse: parse, auth: auth);

  Future<T> post<T>(String path, {Object? data, required T Function(Object? json) parse, bool auth = true}) =>
      _send('POST', path, data: data, parse: parse, auth: auth);

  Future<void> put(String path, {Object? data}) => _send('PUT', path, data: data, parse: (_) {});

  Future<void> delete(String path) => _send('DELETE', path, parse: (_) {});

  Future<T> _send<T>(
    String method,
    String path, {
    Object? data,
    Map<String, Object?>? query,
    required T Function(Object? json) parse,
    bool auth = true,
  }) async {
    final Response<Object?> response;
    try {
      response = await _dio.request<Object?>(
        path,
        data: data,
        queryParameters: query,
        options: Options(method: method, extra: {AuthInterceptor.skipAuthKey: !auth}),
      );
    } on DioException catch (e) {
      throw _fromDio(e);
    }

    try {
      return parse(response.data);
    } catch (_) {
      throw const AppException('Dữ liệu từ máy chủ không hợp lệ');
    }
  }

  static AppException _fromDio(DioException e) {
    switch (e.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return const AppException('Kết nối quá thời gian, vui lòng thử lại');
      case DioExceptionType.connectionError:
        return const AppException('Không kết nối được máy chủ. Kiểm tra mạng hoặc backend');
      case DioExceptionType.cancel:
        return const AppException('Yêu cầu đã bị huỷ');
      case DioExceptionType.badResponse:
        final status = e.response?.statusCode;
        return AppException(_messageForStatus(status, e.response?.data), statusCode: status);
      case DioExceptionType.badCertificate:
      case DioExceptionType.unknown:
        return const AppException('Không kết nối được máy chủ');
    }
  }

  static String _messageForStatus(int? status, Object? body) {
    // Backend trả ProblemDetails: { title, detail, errors: { field: [msg] } }
    final problem = body is Json ? body : const <String, dynamic>{};
    final detail = problem['detail'] as String?;
    return switch (status) {
      400 => _firstValidationError(problem['errors']) ?? problem['title'] as String? ?? 'Yêu cầu không hợp lệ',
      401 => detail ?? 'Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại',
      404 => 'Không tìm thấy nội dung',
      409 => detail ?? 'Dữ liệu bị trùng',
      429 => 'Bạn thao tác quá nhanh, vui lòng thử lại sau ít phút',
      502 || 503 || 504 => 'Nguồn phim tạm thời không phản hồi, vui lòng thử lại sau',
      _ => 'Máy chủ gặp lỗi${status == null ? '' : ' ($status)'}',
    };
  }

  static String? _firstValidationError(Object? errors) {
    if (errors is! Json) return null;
    for (final messages in errors.values) {
      if (messages is List && messages.isNotEmpty && messages.first is String) return messages.first as String;
    }
    return null;
  }
}
