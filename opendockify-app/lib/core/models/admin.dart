class SettingView {
  const SettingView({
    required this.key,
    this.value,
    required this.source,
    required this.isSecret,
    required this.masked,
  });

  final String key;
  final String? value;
  final String source;
  final bool isSecret;
  final bool masked;

  factory SettingView.fromJson(Map<String, dynamic> json) => SettingView(
        key: json['key']?.toString() ?? '',
        value: json['value']?.toString(),
        source: json['source']?.toString() ?? '',
        isSecret: json['isSecret'] == true,
        masked: json['masked'] == true,
      );
}

class UpdateSettingRequest {
  const UpdateSettingRequest(this.value);

  final String value;

  Map<String, dynamic> toJson() => {'value': value};
}

class AiUsageEntry {
  const AiUsageEntry({
    required this.id,
    this.userId,
    this.action,
    this.requestSnippet,
    this.responseSnippet,
    required this.success,
    this.timestamp,
  });

  final String id;
  final String? userId;
  final String? action;
  final String? requestSnippet;
  final String? responseSnippet;
  final bool success;
  final DateTime? timestamp;

  factory AiUsageEntry.fromJson(Map<String, dynamic> json) => AiUsageEntry(
        id: json['id']?.toString() ?? '',
        userId: json['userId']?.toString(),
        action: json['action']?.toString(),
        requestSnippet: json['requestSnippet']?.toString(),
        responseSnippet: json['responseSnippet']?.toString(),
        success: json['success'] == true,
        timestamp: DateTime.tryParse(json['timestamp']?.toString() ?? ''),
      );
}