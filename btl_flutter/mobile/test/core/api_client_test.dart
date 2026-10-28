import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/core/api_client.dart';

/// Adapter giả: trả status + body cố định, hoặc ném lỗi kết nối.
class _FakeAdapter implements HttpClientAdapter {
  _FakeAdapter({this.status = 200, this.body, this.connectionError = false});

  final int status;
  final Object? body;
  final bool connectionError;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    if (connectionError) {
      throw DioException.connectionError(requestOptions: options, reason: 'refused');
    }
    return ResponseBody.fromString(
      jsonEncode(body),
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

ApiClient _client(_FakeAdapter adapter) =>
    ApiClient(Dio(BaseOptions(baseUrl: 'http://test'))..httpClientAdapter = adapter);

Future<AppException> _errorOf(ApiClient client) async {
  try {
    await client.get('/x', parse: (json) => (json as Json)['name'] as String);
  } on AppException catch (e) {
    return e;
  }
  fail('Không ném AppException');
}

void main() {
  test('Thành công → parse dữ liệu', () async {
    final name = await _client(_FakeAdapter(body: {'name': 'VsMovie'}))
        .get('/x', parse: (json) => (json as Json)['name'] as String);

    expect(name, 'VsMovie');
  });

  test('404 → "Không tìm thấy nội dung"', () async {
    final error = await _errorOf(_client(_FakeAdapter(status: 404, body: {'title': 'Không tìm thấy'})));

    expect(error.statusCode, 404);
    expect(error.message, 'Không tìm thấy nội dung');
  });

  test('400 → dùng title của ProblemDetails', () async {
    final error = await _errorOf(_client(_FakeAdapter(status: 400, body: {'title': 'Tham số không hợp lệ'})));

    expect(error.message, 'Tham số không hợp lệ');
  });

  test('502 → nguồn phim không phản hồi', () async {
    final error = await _errorOf(_client(_FakeAdapter(status: 502, body: {'title': 'Lỗi nguồn dữ liệu'})));

    expect(error.message, contains('Nguồn phim tạm thời không phản hồi'));
  });

  test('Không kết nối được → thông điệp kiểm tra mạng/backend', () async {
    final error = await _errorOf(_client(_FakeAdapter(connectionError: true)));

    expect(error.message, contains('Không kết nối được máy chủ'));
  });

  test('JSON sai cấu trúc → "Dữ liệu từ máy chủ không hợp lệ"', () async {
    final error = await _errorOf(_client(_FakeAdapter(body: [1, 2, 3])));

    expect(error.message, 'Dữ liệu từ máy chủ không hợp lệ');
  });
}
