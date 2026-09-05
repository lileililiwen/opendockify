import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/document.dart';
import 'package:opendockify_app/core/storage/draft_store.dart';

class _DelayedDraftStore implements DraftStore {
  final values = <String, LocalDocumentDraft>{};
  final firstWriteStarted = Completer<void>();
  final releaseFirstWrite = Completer<void>();

  @override
  Future<void> clear(String key) async => values.remove(key);

  @override
  Future<LocalDocumentDraft?> read(String key) async => values[key];

  @override
  Future<void> write(String key, LocalDocumentDraft draft) async {
    if (draft.revision == 1) {
      firstWriteStarted.complete();
      await releaseFirstWrite.future;
    }
    values[key] = draft;
  }
}

void main() {
  test('older draft completion cannot overwrite a newer revision', () async {
    final store = _DelayedDraftStore();
    final versioned = RevisionedDraftStore(store);

    final first = versioned.write(
      'form',
      LocalDocumentDraft(
        values: const {'name': 'old'},
        selectedClauseIds: const [],
        updatedAt: DateTime.utc(2026, 1, 1),
        revision: 1,
      ),
    );
    await store.firstWriteStarted.future;
    final second = versioned.write(
      'form',
      LocalDocumentDraft(
        values: const {'name': 'new'},
        selectedClauseIds: const [],
        updatedAt: DateTime.utc(2026, 1, 1),
        revision: 2,
      ),
    );
    store.releaseFirstWrite.complete();

    await Future.wait([first, second]);
    expect((await versioned.read('form'))?.values['name'], 'new');
    expect((await versioned.read('form'))?.revision, 2);
  });

  test(
    'a lower revision requested after a newer revision is ignored',
    () async {
      final store = _DelayedDraftStore();
      final versioned = RevisionedDraftStore(store);
      await versioned.write(
        'form',
        LocalDocumentDraft(
          values: const {'name': 'new'},
          selectedClauseIds: const [],
          updatedAt: DateTime.utc(2026, 1, 1),
          revision: 2,
        ),
      );
      await versioned.write(
        'form',
        LocalDocumentDraft(
          values: const {'name': 'old'},
          selectedClauseIds: const [],
          updatedAt: DateTime.utc(2026, 1, 1),
          revision: 1,
        ),
      );
      expect((await versioned.read('form'))?.values['name'], 'new');
    },
  );
}
