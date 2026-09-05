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
    required this.title,
    required this.templateId,
    required this.templateName,
    required this.isArchived,
    this.parentId,
    required this.status,
    required this.signingStatus,
    required this.createdAt,
  });

  final String id;
  final String title;
  final String templateId;
  final String templateName;
  final bool isArchived;
  final String? parentId;
  final String status;
  final String signingStatus;
  final DateTime? createdAt;

  factory DocumentSummary.fromJson(Map<String, dynamic> json) =>
      DocumentSummary(
        id: json['id']?.toString() ?? '',
        title: json['title']?.toString() ?? '',
        templateId: json['templateId']?.toString() ?? '',
        templateName: json['templateName']?.toString() ?? '',
        isArchived: json['isArchived'] == true,
        parentId: json['parentId']?.toString(),
        status: json['status']?.toString() ?? '',
        signingStatus: json['signingStatus']?.toString() ?? '',
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
      );
}

class DocumentView {
  const DocumentView({
    required this.id,
    required this.title,
    required this.templateId,
    required this.templateName,
    required this.isArchived,
    this.parentId,
    required this.status,
    required this.signingStatus,
    required this.snapshotJson,
    required this.renderedText,
    required this.createdAt,
    this.downloadUrl,
    this.isOwner = true,
    this.accessLevel = 'owner',
  });

  final String id;
  final String title;
  final String templateId;
  final String templateName;
  final bool isArchived;
  final String? parentId;
  final String status;
  final String signingStatus;
  final String snapshotJson;
  final String renderedText;
  final DateTime? createdAt;
  final String? downloadUrl;
  final bool isOwner;
  final String accessLevel;

  factory DocumentView.fromJson(Map<String, dynamic> json) => DocumentView(
    id: json['id']?.toString() ?? '',
    title: json['title']?.toString() ?? '',
    templateId: json['templateId']?.toString() ?? '',
    templateName: json['templateName']?.toString() ?? '',
    isArchived: json['isArchived'] == true,
    parentId: json['parentId']?.toString(),
    status: json['status']?.toString() ?? '',
    signingStatus: json['signingStatus']?.toString() ?? '',
    snapshotJson: json['snapshotJson']?.toString() ?? '',
    renderedText: json['renderedText']?.toString() ?? '',
    createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
    downloadUrl: json['downloadUrl']?.toString(),
    isOwner: json['isOwner'] != false,
    accessLevel: json['accessLevel']?.toString() ?? 'owner',
  );
}

class DocumentShareItem {
  const DocumentShareItem({
    required this.kind,
    required this.id,
    this.username,
    this.accessLevel,
    this.allowDownload,
    this.expiresAt,
    required this.createdAt,
    this.revokedAt,
  });
  final String kind;
  final String id;
  final String? username;
  final String? accessLevel;
  final bool? allowDownload;
  final DateTime? expiresAt;
  final DateTime? createdAt;
  final DateTime? revokedAt;
  factory DocumentShareItem.fromJson(Map<String, dynamic> json) =>
      DocumentShareItem(
        kind: json['kind']?.toString() ?? '',
        id: json['id']?.toString() ?? '',
        username: json['username']?.toString(),
        accessLevel: json['accessLevel']?.toString(),
        allowDownload: json['allowDownload'] as bool?,
        expiresAt: DateTime.tryParse(json['expiresAt']?.toString() ?? ''),
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
        revokedAt: DateTime.tryParse(json['revokedAt']?.toString() ?? ''),
      );
}

class CreatedDocumentShareLink {
  const CreatedDocumentShareLink({
    required this.id,
    required this.token,
    required this.expiresAt,
    required this.allowDownload,
  });
  final String id;
  final String token;
  final DateTime? expiresAt;
  final bool allowDownload;
  factory CreatedDocumentShareLink.fromJson(Map<String, dynamic> json) =>
      CreatedDocumentShareLink(
        id: json['id']?.toString() ?? '',
        token: json['token']?.toString() ?? '',
        expiresAt: DateTime.tryParse(json['expiresAt']?.toString() ?? ''),
        allowDownload: json['allowDownload'] == true,
      );
}

class ShareAuditEntry {
  const ShareAuditEntry({
    required this.action,
    required this.actorCategory,
    required this.succeeded,
    required this.createdAt,
  });
  final String action;
  final String actorCategory;
  final bool succeeded;
  final DateTime? createdAt;
  factory ShareAuditEntry.fromJson(Map<String, dynamic> json) =>
      ShareAuditEntry(
        action: json['action']?.toString() ?? '',
        actorCategory: json['actorCategory']?.toString() ?? '',
        succeeded: json['succeeded'] == true,
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? ''),
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

  factory DocumentListPage.fromJson(Map<String, dynamic> json) =>
      DocumentListPage(
        items: (json['items'] is List ? json['items'] as List : const [])
            .map(
              (e) => DocumentSummary.fromJson(
                e is Map<String, dynamic> ? e : <String, dynamic>{},
              ),
            )
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
    final doc = docJson is Map<String, dynamic>
        ? DocumentView.fromJson(docJson)
        : null;
    return GenerateResult(
      document: doc ?? DocumentView.fromJson(const {}),
      warnings: warnings,
      downloadUrl: json['downloadUrl']?.toString(),
    );
  }
}

class PreviewResult {
  const PreviewResult({
    required this.templateName,
    required this.renderedText,
    this.warnings = const [],
  });

  final String templateName;
  final String renderedText;
  final List<String> warnings;

  factory PreviewResult.fromJson(Map<String, dynamic> json) => PreviewResult(
    templateName: json['templateName']?.toString() ?? '',
    renderedText: json['renderedText']?.toString() ?? '',
    warnings: (json['warnings'] is List ? json['warnings'] as List : const [])
        .whereType<String>()
        .toList(),
  );
}

class LocalDocumentDraft {
  const LocalDocumentDraft({
    required this.values,
    required this.selectedClauseIds,
    required this.updatedAt,
    this.interviewSessionId,
    this.revision = 0,
  });

  final Map<String, String> values;
  final List<String> selectedClauseIds;
  final DateTime updatedAt;
  final String? interviewSessionId;
  final int revision;

  Map<String, dynamic> toJson() => {
    'values': values,
    'selectedClauseIds': selectedClauseIds,
    'updatedAt': updatedAt.toUtc().toIso8601String(),
    'revision': revision,
    if (interviewSessionId != null) 'interviewSessionId': interviewSessionId,
  };

  factory LocalDocumentDraft.fromJson(Map<String, dynamic> json) {
    final values = json['values'];
    final clauses = json['selectedClauseIds'];
    return LocalDocumentDraft(
      values: values is Map
          ? values.map(
              (key, value) => MapEntry(key.toString(), value?.toString() ?? ''),
            )
          : const {},
      selectedClauseIds: clauses is List
          ? clauses.whereType<String>().toList()
          : const [],
      updatedAt:
          DateTime.tryParse(json['updatedAt']?.toString() ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      interviewSessionId: json['interviewSessionId']?.toString(),
      revision: (json['revision'] as num?)?.toInt() ?? 0,
    );
  }
}

abstract final class DraftStorageKey {
  static String forForm({required String userId, required String formId}) {
    return 'document_draft_${userId}_$formId';
  }
}
