import '../../core/api_client.dart';

List<T> parseList<T>(Object? value, T Function(Json json) parse) =>
    (value as List<dynamic>? ?? const []).map((e) => parse(e as Json)).toList(growable: false);

List<String> parseStrings(Object? value) =>
    (value as List<dynamic>? ?? const []).whereType<String>().toList(growable: false);

int? parseInt(Object? value) => (value as num?)?.toInt();

double? parseDouble(Object? value) => (value as num?)?.toDouble();
