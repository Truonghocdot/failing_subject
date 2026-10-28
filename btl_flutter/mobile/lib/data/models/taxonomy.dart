import '../../core/api_client.dart';
import 'json_helpers.dart';

/// Thể loại hoặc quốc gia.
class Taxonomy {
  const Taxonomy({required this.id, required this.name, required this.slug});

  factory Taxonomy.fromJson(Json json) =>
      Taxonomy(id: parseInt(json['id']) ?? 0, name: json['name'] as String, slug: json['slug'] as String);

  final int id;
  final String name;
  final String slug;
}
