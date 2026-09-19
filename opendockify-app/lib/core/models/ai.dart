class PolishResult {
  const PolishResult({required this.text, required this.warning, this.remainingTokens});

  final String text;
  final String warning;

  /// Remaining per-day AI tokens for the user, when the server enforces
  /// `Ai:MaxTokensPerDay`. Null when the budget is disabled.
  final int? remainingTokens;

  factory PolishResult.fromJson(Map<String, dynamic> json) => PolishResult(
        text: json['text']?.toString() ?? '',
        warning: json['warning']?.toString() ?? '',
        remainingTokens: json['remainingTokens'] is num ? (json['remainingTokens'] as num).toInt() : null,
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