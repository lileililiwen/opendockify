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
      final json = GenerateDocumentRequest(
        templateId: 't1',
        values: const {},
      ).toJson();
      expect(json['selectedClauseIds'], isEmpty);
    });
  });

  group('DocumentListPage.fromJson', () {
    test('parses a paged list', () {
      final page = DocumentListPage.fromJson(const {
        'items': [
          {
            'id': 'd1',
            'title': 'Loan with Alice',
            'templateId': 't1',
            'templateName': 'Loan IOU',
            'isArchived': true,
            'parentId': null,
            'status': 'Generated',
            'signingStatus': 'NotStarted',
            'createdAt': '2026-01-01T00:00:00Z',
          },
        ],
        'page': 1,
        'pageSize': 20,
        'totalCount': 1,
        'totalPages': 1,
      });
      expect(page.items, hasLength(1));
      expect(page.items.first.id, 'd1');
      expect(page.items.first.title, 'Loan with Alice');
      expect(page.items.first.templateName, 'Loan IOU');
      expect(page.items.first.isArchived, isTrue);
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

  group('PreviewResult.fromJson', () {
    test('parses rendered text and warnings without document identity', () {
      final preview = PreviewResult.fromJson(const {
        'templateName': 'Loan IOU',
        'renderedText': 'Preview text',
        'warnings': ['Rate warning'],
      });
      expect(preview.templateName, 'Loan IOU');
      expect(preview.renderedText, 'Preview text');
      expect(preview.warnings, ['Rate warning']);
    });
  });

  group('LocalDocumentDraft', () {
    test('round trips values, clauses and timestamp', () {
      final draft = LocalDocumentDraft(
        values: const {'amount': '1000'},
        selectedClauseIds: const ['cl1'],
        updatedAt: DateTime.utc(2026, 1, 1),
      );
      final restored = LocalDocumentDraft.fromJson(draft.toJson());
      expect(restored.values, {'amount': '1000'});
      expect(restored.selectedClauseIds, ['cl1']);
      expect(restored.updatedAt, DateTime.utc(2026, 1, 1));
    });

    test('storage keys isolate users and form identities', () {
      final first = DraftStorageKey.forForm(
        userId: 'user-a',
        formId: 'template-1',
      );
      final secondUser = DraftStorageKey.forForm(
        userId: 'user-b',
        formId: 'template-1',
      );
      final secondForm = DraftStorageKey.forForm(
        userId: 'user-a',
        formId: 'template-2',
      );
      expect(first, isNot(secondUser));
      expect(first, isNot(secondForm));
    });
  });

  group('DocumentView.fromJson', () {
    test('parses snapshot shape used for re-edit', () {
      final doc = DocumentView.fromJson(const {
        'id': 'd1',
        'title': 'Loan with Alice',
        'templateId': 't1',
        'templateName': 'Loan IOU',
        'isArchived': false,
        'parentId': 'd0',
        'status': 'Generated',
        'signingStatus': 'Signed',
        'snapshotJson':
            '{"values":{"amount":"1000"},"selectedClauseIds":["cl1"]}',
        'renderedText': 'text',
        'createdAt': '2026-01-01T00:00:00Z',
        'downloadUrl': '/api/documents/d1/download',
      });
      expect(doc.snapshotJson, contains('selectedClauseIds'));
      expect(doc.title, 'Loan with Alice');
      expect(doc.templateName, 'Loan IOU');
      expect(doc.isArchived, isFalse);
      expect(doc.parentId, 'd0');
      expect(doc.downloadUrl, isNotNull);
    });
  });
}
