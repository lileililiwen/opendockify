class GenerateDocumentRequest {
  const GenerateDocumentRequest({
    required this.templateId,
    required this.values,
    this.selectedClauseIds = const [],
  });

  final String templateId;
  final Map<String, String> values;
  final List<String> selectedClauseIds;

  Map<String, dynamic> toJson() => {
        'templateId': templateId,
        'values': values,
        'selectedClauseIds': selectedClauseIds,
      };
}

class DocumentSummary {
  const DocumentSummary({
    required this.id,
    required this.templateId,
    this.parentId,
    required this.status,
    required this.signingStatus,
    required this.createdAt,
  });

  final String id;
  final String templateId;
  final String? parentId;
  final String status;
  final String signingStatus;
  final DateTime? createdAt;

  factory DocumentSummary.fromJson(Map<String, dynamic> json) => DocumentSummary(
        id: json['id']?.toString() ?? '',
        templateId: json['templateId']?.toString() ?? '',
        parentId: json['parentId']?.toString(),
        status: json['status']?.toString() ?? '',
        signingStatus: json['signingStatus']?.toString() ?? '',
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
      );
}

class DocumentView {
  const DocumentView({
    required this.id,
    required this.templateId,
    this.parentId,
    required this.status,
    required this.signingStatus,
    required this.snapshotJson,
    required this.renderedText,
    required this.createdAt,
    this.downloadUrl,
  });

  final String id;
  final String templateId;
  final String? parentId;
  final String status;
  final String signingStatus;
  final String snapshotJson;
  final String renderedText;
  final DateTime? createdAt;
  final String? downloadUrl;

  factory DocumentView.fromJson(Map<String, dynamic> json) => DocumentView(
        id: json['id']?.toString() ?? '',
        templateId: json['templateId']?.toString() ?? '',
        parentId: json['parentId']?.toString(),
        status: json['status']?.toString() ?? '',
        signingStatus: json['signingStatus']?.toString() ?? '',
        snapshotJson: json['snapshotJson']?.toString() ?? '',
        renderedText: json['renderedText']?.toString() ?? '',
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
        downloadUrl: json['downloadUrl']?.toString(),
      );
}

class DocumentListPage {
  const DocumentListPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<DocumentSummary> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  bool get hasMore => page < totalPages;

  factory DocumentListPage.fromJson(Map<String, dynamic> json) => DocumentListPage(
        items: (json['items'] is List ? json['items'] as List : const [])
            .map((e) => DocumentSummary.fromJson(e is Map<String, dynamic> ? e : <String, dynamic>{}))
            .toList(),
        page: (json['page'] as num?)?.toInt() ?? 1,
        pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
        totalPages: (json['totalPages'] as num?)?.toInt() ?? 0,
      );
}

class GenerateResult {
  const GenerateResult({
    required this.document,
    this.warnings = const [],
    this.downloadUrl,
  });

  final DocumentView document;
  final List<String> warnings;
  final String? downloadUrl;

  factory GenerateResult.fromJson(Map<String, dynamic> json) {
    final docJson = json['document'];
    final warningsRaw = json['warnings'];
    final warnings = warningsRaw is List
        ? warningsRaw.whereType<String>().toList()
        : <String>[];
    final doc = docJson is Map<String, dynamic> ? DocumentView.fromJson(docJson) : null;
    return GenerateResult(
      document: doc ?? DocumentView.fromJson(const {}),
      warnings: warnings,
      downloadUrl: json['downloadUrl']?.toString(),
    );
  }
}