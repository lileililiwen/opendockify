import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/document.dart';
import 'package:opendockify_app/core/storage/draft_store.dart';

class _MemoryDraftStore implements DraftStore {
  final _values = <String, LocalDocumentDraft>{};

  @override
  Future<void> clear(String key) async => _values.remove(key);

  @override
  Future<LocalDocumentDraft?> read(String key) async => _values[key];

  @override
  Future<void> write(String key, LocalDocumentDraft draft) async =>
      _values[key] = draft;
}

void main() {
  test(
    'draft storage restores and clears only the selected user/form key',
    () async {
      final store = _MemoryDraftStore();
      final firstKey = DraftStorageKey.forForm(
        userId: 'user-a',
        formId: 'template-1',
      );
      final otherKey = DraftStorageKey.forForm(
        userId: 'user-b',
        formId: 'template-1',
      );
      final draft = LocalDocumentDraft(
        values: const {'party': 'Alice'},
        selectedClauseIds: const ['cl1'],
        updatedAt: DateTime.utc(2026, 1, 1),
        interviewSessionId: 'session-1',
      );

      await store.write(firstKey, draft);
      expect((await store.read(firstKey))?.values['party'], 'Alice');
      expect((await store.read(firstKey))?.interviewSessionId, 'session-1');
      expect(await store.read(otherKey), isNull);

      await store.clear(firstKey);
      expect(await store.read(firstKey), isNull);
    },
  );
}
