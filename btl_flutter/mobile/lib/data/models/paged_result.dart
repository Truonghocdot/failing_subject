import '../../core/api_client.dart';
import 'json_helpers.dart';

class PagedResult<T> {
  const PagedResult({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalItems,
    required this.totalPages,
  });

  factory PagedResult.fromJson(Json json, T Function(Json json) parseItem) => PagedResult(
    items: parseList(json['items'], parseItem),
    page: parseInt(json['page']) ?? 1,
    pageSize: parseInt(json['pageSize']) ?? 0,
    totalItems: parseInt(json['totalItems']) ?? 0,
    totalPages: parseInt(json['totalPages']) ?? 0,
  );

  final List<T> items;
  final int page;
  final int pageSize;
  final int totalItems;
  final int totalPages;

  bool get hasMore => page < totalPages;
}
