import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/document.dart';

void main() {
  group('GenerateDocumentRequest', () {
    test('serializes to the snapshot contract', () {
      final request = GenerateDocumentRequest(
        templateId: 't1',
        values: const {'amount': '1000'},
        selectedClauseIds: const ['cl1'],
      );
      final json = request.toJson();
      expect(json['templateId'], 't1');
      expect(json['values'], {'amount': '1000'});
      expect(json['selectedClauseIds'], ['cl1']);
    });

    test('defaults selectedClauseIds to empty', () {
      final json = GenerateDocumentRequest(templateId: 't1', values: const {}).toJson();
      expect(json['selectedClauseIds'], isEmpty);
    });
  });

  group('DocumentListPage.fromJson', () {
    test('parses a paged list', () {
      final page = DocumentListPage.fromJson(const {
        'items': [
          {
            'id': 'd1',
            'templateId': 't1',
            'parentId': null,
            'status': 'Generated',
            'signingStatus': 'NotStarted',
            'createdAt': '2026-01-01T00:00:00Z',
          }
        ],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      });
      expect(page.items, hasLength(1));
      expect(page.items.first.id, 'd1');
      expect(page.items.first.status, 'Generated');
      expect(page.hasMore, isFalse);
    });

    test('computes hasMore', () {
      final page = DocumentListPage.fromJson(const {
        'items': [],
        'page': 1,
        'pageSize': 20,
        'totalCount': 40,
        'totalPages': 2,
      });
      expect(page.hasMore, isTrue);
    });
  });

  group('GenerateResult.fromJson', () {
    test('parses document, warnings and download url', () {
      final result = GenerateResult.fromJson(const {
        'document': {
          'id': 'd1',
          'templateId': 't1',
          'status': 'Generated',
          'signingStatus': 'NotStarted',
          'snapshotJson': '{"values":{}}',
          'renderedText': 'Hello',
        },
        'warnings': ['Risk notice truncated.'],
        'downloadUrl': '/api/documents/d1/download',
      });
      expect(result.document.id, 'd1');
      expect(result.document.renderedText, 'Hello');
      expect(result.warnings, ['Risk notice truncated.']);
      expect(result.downloadUrl, '/api/documents/d1/download');
    });
  });

  group('DocumentView.fromJson', () {
    test('parses snapshot shape used for re-edit', () {
      final doc = DocumentView.fromJson(const {
        'id': 'd1',
        'templateId': 't1',
        'parentId': 'd0',
        'status': 'Generated',
        'signingStatus': 'Signed',
        'snapshotJson': '{"values":{"amount":"1000"},"selectedClauseIds":["cl1"]}',
        'renderedText': 'text',
        'createdAt': '2026-01-01T00:00:00Z',
        'downloadUrl': '/api/documents/d1/download',
      });
      expect(doc.snapshotJson, contains('selectedClauseIds'));
      expect(doc.parentId, 'd0');
      expect(doc.downloadUrl, isNotNull);
    });
  });
}