class TemplateSummary {
  const TemplateSummary({
    required this.id,
    required this.name,
    required this.category,
    required this.description,
    required this.isBuiltIn,
    required this.isPublic,
    this.ownerId,
  });

  final String id;
  final String name;
  final String category;
  final String description;
  final bool isBuiltIn;
  final bool isPublic;
  final String? ownerId;

  factory TemplateSummary.fromJson(Map<String, dynamic> json) => TemplateSummary(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        category: json['category']?.toString() ?? '',
        description: json['description']?.toString() ?? '',
        isBuiltIn: json['isBuiltIn'] == true,
        isPublic: json['isPublic'] == true,
        ownerId: json['ownerId']?.toString(),
      );
}

class Template {
  const Template({
    required this.id,
    required this.name,
    required this.category,
    required this.description,
    required this.riskNoticeText,
    required this.body,
    required this.definitionJson,
    required this.isBuiltIn,
    required this.isPublic,
    this.ownerId,
    this.createdAt,
    this.updatedAt,
  });

  final String id;
  final String name;
  final String category;
  final String description;
  final String riskNoticeText;
  final String body;
  final String definitionJson;
  final bool isBuiltIn;
  final bool isPublic;
  final String? ownerId;
  final DateTime? createdAt;
  final DateTime? updatedAt;

  factory Template.fromJson(Map<String, dynamic> json) => Template(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        category: json['category']?.toString() ?? '',
        description: json['description']?.toString() ?? '',
        riskNoticeText: json['riskNoticeText']?.toString() ?? '',
        body: json['body']?.toString() ?? '',
        definitionJson: json['definitionJson']?.toString() ?? '',
        isBuiltIn: json['isBuiltIn'] == true,
        isPublic: json['isPublic'] == true,
        ownerId: json['ownerId']?.toString(),
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
        updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? ''),
      );
}

class CreateTemplateRequest {
  const CreateTemplateRequest({
    required this.name,
    required this.category,
    this.description,
    this.riskNoticeText,
    required this.body,
    required this.definitionJson,
  });

  final String name;
  final String category;
  final String? description;
  final String? riskNoticeText;
  final String body;
  final String definitionJson;

  Map<String, dynamic> toJson() => {
        'name': name,
        'category': category,
        'description': description,
        'riskNoticeText': riskNoticeText,
        'body': body,
        'definitionJson': definitionJson,
      };
}