import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api_client.dart';
import 'package:mobile/core/auth_interceptor.dart';
import 'package:mobile/core/token_storage.dart';

/// Backend giả: /api/me chỉ chấp nhận [validAccess]; /api/auth/refresh chấp nhận [validRefresh].
class _FakeBackend implements HttpClientAdapter {
  String validAccess = 'access-new';
  String? validRefresh = 'refresh-old';
  bool refreshNetworkDown = false;
  final List<String> calls = [];

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    calls.add('${options.method} ${options.path}');
    if (options.path == '/api/auth/refresh') {
      if (refreshNetworkDown) throw DioException.connectionError(requestOptions: options, reason: 'offline');
      final token = (options.data as Map)['refreshToken'];
      return token == validRefresh
          ? _json(200, {'accessToken': validAccess, 'refreshToken': 'refresh-new'})
          : _json(401, {'detail': 'Phiên đăng nhập không hợp lệ'});
    }
    final auth = options.headers['Authorization'];
    return auth == 'Bearer $validAccess' ? _json(200, {'name': 'An'}) : _json(401, {'title': 'Unauthorized'});
  }

  ResponseBody _json(int status, Object body) => ResponseBody.fromString(
    jsonEncode(body),
    status,
    headers: {
      Headers.contentTypeHeader: [Headers.jsonContentType],
    },
  );

  @override
  void close({bool force = false}) {}
}

void main() {
  late _FakeBackend backend;
  late InMemoryTokenStorage storage;
  late ApiClient client;
  late int expiredEvents;

  setUp(() {
    backend = _FakeBackend();
    storage = InMemoryTokenStorage(const AuthTokens(accessToken: 'access-old', refreshToken: 'refresh-old'));
    expiredEvents = 0;
    final options = BaseOptions(baseUrl: 'http://test');
    final plain = Dio(options)..httpClientAdapter = backend;
    final dio = Dio(options)
      ..httpClientAdapter = backend
      ..interceptors.add(AuthInterceptor(storage: storage, plainDio: plain, onSessionExpired: () => expiredEvents++));
    client = ApiClient(dio);
  });

  Future<String> me() => client.get('/api/me', parse: (json) => (json as Json)['name'] as String);

  test('Access token hết hạn → tự refresh, lưu token mới, gọi lại request', () async {
    expect(await me(), 'An');

    expect(backend.calls, ['GET /api/me', 'POST /api/auth/refresh', 'GET /api/me']);
    expect(storage.tokens?.accessToken, 'access-new');
    expect(storage.tokens?.refreshToken, 'refresh-new');
    expect(expiredEvents, 0);
  });

  test('Nhiều request 401 cùng lúc chỉ refresh một lần', () async {
    final results = await Future.wait([me(), me(), me()]);

    expect(results, ['An', 'An', 'An']);
    expect(backend.calls.where((c) => c.contains('refresh')).length, 1);
  });

  test('Refresh token bị từ chối → xoá token, báo hết phiên, trả 401', () async {
    backend.validRefresh = null;

    await expectLater(me(), throwsA(isA<AppException>().having((e) => e.statusCode, 'status', 401)));
    expect(storage.tokens, isNull);
    expect(expiredEvents, 1);
  });

  test('Mất mạng khi refresh → giữ phiên (không đăng xuất)', () async {
    backend.refreshNetworkDown = true;

    await expectLater(me(), throwsA(isA<AppException>()));
    expect(storage.tokens?.refreshToken, 'refresh-old');
    expect(expiredEvents, 0);
  });

  test('API không cần token (auth: false) không gắn Bearer và không refresh', () async {
    await expectLater(
      client.get('/api/me', auth: false, parse: (json) => json),
      throwsA(isA<AppException>().having((e) => e.statusCode, 'status', 401)),
    );
    expect(backend.calls, ['GET /api/me']);
  });
}
