import 'dart:convert';

enum FieldType {
  text,
  number,
  currency,
  date;

  static FieldType fromApi(String? value) {
    switch (value?.toLowerCase()) {
      case 'number':
        return FieldType.number;
      case 'currency':
        return FieldType.currency;
      case 'date':
        return FieldType.date;
      case 'text':
      default:
        return FieldType.text;
    }
  }
}

class ValidationRule {
  const ValidationRule({
    this.nonNegative = false,
    this.min,
    this.max,
    this.minLength,
    this.maxLength,
    this.pattern,
    this.dateFrom,
    this.dateTo,
    this.isInterestRate = false,
  });

  final bool nonNegative;
  final num? min;
  final num? max;
  final int? minLength;
  final int? maxLength;
  final String? pattern;
  final String? dateFrom;
  final String? dateTo;
  final bool isInterestRate;

  factory ValidationRule.fromJson(Map<String, dynamic>? json) {
    if (json == null) return const ValidationRule();
    num? asNum(Object? value) {
      if (value is num) return value;
      if (value is String) return num.tryParse(value);
      return null;
    }

    int? asInt(Object? value) {
      if (value is int) return value;
      if (value is num) return value.toInt();
      if (value is String) return int.tryParse(value);
      return null;
    }

    return ValidationRule(
      nonNegative: json['nonNegative'] == true,
      min: asNum(json['min']),
      max: asNum(json['max']),
      minLength: asInt(json['minLength']),
      maxLength: asInt(json['maxLength']),
      pattern: json['pattern']?.toString(),
      dateFrom: json['dateFrom']?.toString(),
      dateTo: json['dateTo']?.toString(),
      isInterestRate: json['isInterestRate'] == true,
    );
  }
}

class FieldDefinition {
  const FieldDefinition({
    required this.name,
    required this.label,
    required this.type,
    this.required = false,
    this.validation,
  });

  final String name;
  final String label;
  final FieldType type;
  final bool required;
  final ValidationRule? validation;

  factory FieldDefinition.fromJson(Map<String, dynamic> json) => FieldDefinition(
        name: json['name']?.toString() ?? '',
        label: json['label']?.toString() ?? '',
        type: FieldType.fromApi(json['type']?.toString()),
        required: json['required'] == true,
        validation: ValidationRule.fromJson(
          json['validation'] is Map<String, dynamic> ? json['validation'] as Map<String, dynamic> : null,
        ),
      );
}

class ClauseDefinition {
  const ClauseDefinition({required this.id, required this.title, required this.text});

  final String id;
  final String title;
  final String text;

  factory ClauseDefinition.fromJson(Map<String, dynamic> json) => ClauseDefinition(
        id: json['id']?.toString() ?? '',
        title: json['title']?.toString() ?? '',
        text: json['text']?.toString() ?? '',
      );
}

class TemplateDefinition {
  const TemplateDefinition({this.fields = const [], this.clauses = const []});

  final List<FieldDefinition> fields;
  final List<ClauseDefinition> clauses;

  factory TemplateDefinition.fromJson(Map<String, dynamic> json) => TemplateDefinition(
        fields: _asList(json['fields'])
            .map((e) => FieldDefinition.fromJson(e is Map<String, dynamic> ? e : <String, dynamic>{}))
            .toList(),
        clauses: _asList(json['clauses'])
            .map((e) => ClauseDefinition.fromJson(e is Map<String, dynamic> ? e : <String, dynamic>{}))
            .toList(),
      );

  static List<dynamic> _asList(Object? value) => value is List ? value : const [];
}

class TemplateDefinitionParseException implements Exception {
  TemplateDefinitionParseException(this.message);
  final String message;

  @override
  String toString() => 'TemplateDefinitionParseException: $message';
}

/// Parses a template's `definitionJson` string. Throws
/// [TemplateDefinitionParseException] on malformed JSON so the caller can
/// surface a friendly error instead of crashing.
class TemplateDefinitionParser {
  TemplateDefinitionParser._();

  static TemplateDefinition parse(String definitionJson) {
    if (definitionJson.trim().isEmpty) {
      throw TemplateDefinitionParseException('Template definition is empty.');
    }
    try {
      final decoded = jsonDecode(definitionJson);
      if (decoded is! Map<String, dynamic>) {
        throw TemplateDefinitionParseException('Template definition must be a JSON object.');
      }
      return TemplateDefinition.fromJson(decoded);
    } on FormatException {
      throw TemplateDefinitionParseException('Template definition is not valid JSON.');
    }
  }
}