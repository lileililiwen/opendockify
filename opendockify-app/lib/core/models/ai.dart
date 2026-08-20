class PolishResult {
  const PolishResult({required this.text, required this.warning});

  final String text;
  final String warning;

  factory PolishResult.fromJson(Map<String, dynamic> json) => PolishResult(
        text: json['text']?.toString() ?? '',
        warning: json['warning']?.toString() ?? '',
      );
}

class PolishClauseRequest {
  const PolishClauseRequest({required this.templateId, this.draft});

  final String templateId;
  final String? draft;

  Map<String, dynamic> toJson() => {'templateId': templateId, 'draft': draft};
}

class PolishDocumentRequest {
  const PolishDocumentRequest({
    required this.templateId,
    this.renderedText,
    this.values = const {},
    this.selectedClauseIds = const [],
  });

  final String templateId;
  final String? renderedText;
  final Map<String, String> values;
  final List<String> selectedClauseIds;

  Map<String, dynamic> toJson() => {
        'templateId': templateId,
        'renderedText': renderedText,
        'values': values,
        'selectedClauseIds': selectedClauseIds,
      };
}