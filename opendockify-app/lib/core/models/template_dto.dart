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

  factory TemplateSummary.fromJson(Map<String, dynamic> json) =>
      TemplateSummary(
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
    this.stableId = '',
    this.currentRevision = 1,
    this.sourceInstance,
    this.sourceStableId,
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
  final String stableId;
  final int currentRevision;
  final String? sourceInstance;
  final String? sourceStableId;

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
    stableId: json['stableId']?.toString() ?? '',
    currentRevision: (json['currentRevision'] as num?)?.toInt() ?? 1,
    sourceInstance: json['sourceInstance']?.toString(),
    sourceStableId: json['sourceStableId']?.toString(),
  );
}

class TemplateRevision {
  const TemplateRevision({required this.revision, required this.createdAt});
  final int revision;
  final DateTime? createdAt;
  factory TemplateRevision.fromJson(Map<String, dynamic> json) =>
      TemplateRevision(
        revision: (json['revision'] as num?)?.toInt() ?? 0,
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
      );
}

class PackageValidation {
  const PackageValidation({
    required this.receipt,
    required this.conflict,
    required this.digest,
  });
  final String receipt;
  final bool conflict;
  final String digest;
  factory PackageValidation.fromJson(Map<String, dynamic> json) =>
      PackageValidation(
        receipt: json['receipt']?.toString() ?? '',
        conflict: json['conflict'] == true,
        digest: json['digest']?.toString() ?? '',
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
