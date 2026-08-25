import 'document.dart';
import 'template.dart';

class InterviewStep {
  const InterviewStep({
    required this.id,
    required this.title,
    required this.fields,
    required this.position,
    required this.total,
    this.reviewLabel,
  });

  final String id;
  final String title;
  final String? reviewLabel;
  final List<FieldDefinition> fields;
  final int position;
  final int total;

  factory InterviewStep.fromJson(Map<String, dynamic> json) => InterviewStep(
    id: json['id']?.toString() ?? '',
    title: json['title']?.toString() ?? '',
    reviewLabel: json['reviewLabel']?.toString(),
    fields: (json['fields'] is List ? json['fields'] as List : const [])
        .whereType<Map>()
        .map(
          (item) => FieldDefinition.fromJson(Map<String, dynamic>.from(item)),
        )
        .toList(),
    position: json['position'] is int ? json['position'] as int : 0,
    total: json['total'] is int ? json['total'] as int : 0,
  );
}

class InterviewSession {
  const InterviewSession({
    required this.id,
    required this.templateId,
    required this.version,
    required this.expiresAt,
    required this.answers,
    required this.selectedClauseIds,
    required this.readyForReview,
    this.currentStep,
  });

  final String id;
  final String templateId;
  final int version;
  final DateTime? expiresAt;
  final Map<String, String> answers;
  final List<String> selectedClauseIds;
  final InterviewStep? currentStep;
  final bool readyForReview;

  factory InterviewSession.fromJson(Map<String, dynamic> json) =>
      InterviewSession(
        id: json['id']?.toString() ?? '',
        templateId: json['templateId']?.toString() ?? '',
        version: json['version'] is int ? json['version'] as int : 0,
        expiresAt: DateTime.tryParse(json['expiresAt']?.toString() ?? ''),
        answers: json['answers'] is Map
            ? (json['answers'] as Map).map(
                (key, value) =>
                    MapEntry(key.toString(), value?.toString() ?? ''),
              )
            : const {},
        selectedClauseIds:
            (json['selectedClauseIds'] is List
                    ? json['selectedClauseIds'] as List
                    : const [])
                .map((item) => item.toString())
                .toList(),
        currentStep: json['currentStep'] is Map<String, dynamic>
            ? InterviewStep.fromJson(
                json['currentStep'] as Map<String, dynamic>,
              )
            : null,
        readyForReview: json['readyForReview'] == true,
      );
}

class InterviewReviewItem {
  const InterviewReviewItem({
    required this.stepId,
    required this.label,
    required this.answers,
  });

  final String stepId;
  final String label;
  final Map<String, String> answers;

  factory InterviewReviewItem.fromJson(Map<String, dynamic> json) =>
      InterviewReviewItem(
        stepId: json['stepId']?.toString() ?? '',
        label: json['label']?.toString() ?? '',
        answers: json['answers'] is Map
            ? (json['answers'] as Map).map(
                (key, value) =>
                    MapEntry(key.toString(), value?.toString() ?? ''),
              )
            : const {},
      );
}

class InterviewResponse {
  const InterviewResponse({this.session, this.review = const [], this.preview});

  final InterviewSession? session;
  final List<InterviewReviewItem> review;
  final PreviewResult? preview;

  factory InterviewResponse.fromJson(Map<String, dynamic> json) =>
      InterviewResponse(
        session: json['session'] is Map<String, dynamic>
            ? InterviewSession.fromJson(json['session'] as Map<String, dynamic>)
            : null,
        review: (json['review'] is List ? json['review'] as List : const [])
            .whereType<Map>()
            .map(
              (item) =>
                  InterviewReviewItem.fromJson(Map<String, dynamic>.from(item)),
            )
            .toList(),
        preview: json['preview'] is Map<String, dynamic>
            ? PreviewResult.fromJson(json['preview'] as Map<String, dynamic>)
            : null,
      );
}
