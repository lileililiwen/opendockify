import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/template_dto.dart';

void main() {
  group('TemplateSummary.fromJson', () {
    test('parses marketplace summary', () {
      final summary = TemplateSummary.fromJson(const {
        'id': 't1',
        'name': 'Loan Agreement',
        'category': 'finance',
        'description': 'A standard loan',
        'isBuiltIn': true,
        'isPublic': true,
        'ownerId': 'u1',
      });
      expect(summary.id, 't1');
      expect(summary.name, 'Loan Agreement');
      expect(summary.category, 'finance');
      expect(summary.isBuiltIn, isTrue);
      expect(summary.isPublic, isTrue);
      expect(summary.ownerId, 'u1');
    });

    test('defaults missing fields', () {
      final summary = TemplateSummary.fromJson(const {});
      expect(summary.id, '');
      expect(summary.name, '');
      expect(summary.ownerId, isNull);
      expect(summary.isBuiltIn, isFalse);
    });
  });

  group('Template.fromJson', () {
    test('parses full template with dates', () {
      final template = Template.fromJson(const {
        'id': 't1',
        'name': 'Loan Agreement',
        'category': 'finance',
        'description': 'desc',
        'riskNoticeText': 'notice',
        'body': 'body {{x}}',
        'definitionJson': '{"fields":[]}',
        'isBuiltIn': false,
        'isPublic': true,
        'ownerId': null,
        'createdAt': '2026-01-02T03:04:05Z',
        'stableId': 'stable-1',
        'currentRevision': 3,
        'sourceInstance': 'legal.example',
      });
      expect(template.id, 't1');
      expect(template.body, 'body {{x}}');
      expect(template.definitionJson, '{"fields":[]}');
      expect(template.isPublic, isTrue);
      expect(template.ownerId, isNull);
      expect(template.createdAt, DateTime.utc(2026, 1, 2, 3, 4, 5));
      expect(template.stableId, 'stable-1');
      expect(template.currentRevision, 3);
      expect(template.sourceInstance, 'legal.example');
    });
  });

  group('CreateTemplateRequest', () {
    test('serializes to the backend contract', () {
      const request = CreateTemplateRequest(
        name: 'Loan Agreement',
        category: 'finance',
        description: 'desc',
        riskNoticeText: 'notice',
        body: 'body {{amount}}',
        definitionJson: '{"fields":[]}',
      );
      final json = request.toJson();
      expect(json['name'], 'Loan Agreement');
      expect(json['category'], 'finance');
      expect(json['description'], 'desc');
      expect(json['riskNoticeText'], 'notice');
      expect(json['body'], 'body {{amount}}');
      expect(json['definitionJson'], '{"fields":[]}');
    });

    test('allows nullable description fields', () {
      const request = CreateTemplateRequest(
        name: 'n',
        category: 'c',
        body: 'b',
        definitionJson: '{}',
      );
      expect(request.toJson()['description'], isNull);
      expect(request.toJson()['riskNoticeText'], isNull);
    });
  });

  test('package validation exposes receipt and conflict', () {
    final validation = PackageValidation.fromJson(const {
      'receipt': 'receipt-1',
      'conflict': true,
      'digest': 'abc',
    });
    expect(validation.receipt, 'receipt-1');
    expect(validation.conflict, isTrue);
    expect(validation.digest, 'abc');
  });
}
