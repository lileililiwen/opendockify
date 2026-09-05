import '../models/document.dart';

/// User/form-scoped storage for sensitive in-progress document snapshots.
abstract class DraftStore {
  Future<LocalDocumentDraft?> read(String key);
  Future<void> write(String key, LocalDocumentDraft draft);
  Future<void> clear(String key);
}

/// Serializes writes per form so an older asynchronous completion cannot
/// overwrite a newer revision. The wrapper deliberately preserves the
/// existing DraftStore API for callers and test doubles.
class RevisionedDraftStore implements DraftStore {
  RevisionedDraftStore(this._inner);

  final DraftStore _inner;
  final Map<String, Future<void>> _writeTails = {};
  final Map<String, int> _latestRequested = {};

  @override
  Future<LocalDocumentDraft?> read(String key) => _inner.read(key);

  @override
  Future<void> write(String key, LocalDocumentDraft draft) {
    final latest = _latestRequested[key] ?? -1;
    if (draft.revision < latest) return Future<void>.value();
    _latestRequested[key] = draft.revision;
    final previous = _writeTails[key] ?? Future<void>.value();
    final operation = previous.then((_) {
      if (draft.revision < (_latestRequested[key] ?? draft.revision)) {
        return Future<void>.value();
      }
      return _inner.write(key, draft);
    });
    final tail = operation.catchError((_) {});
    _writeTails[key] = tail;
    return operation.whenComplete(() {
      if (identical(_writeTails[key], tail)) {
        _writeTails.remove(key);
      }
    });
  }

  @override
  Future<void> clear(String key) async {
    final previous = _writeTails[key];
    if (previous != null) await previous;
    _writeTails.remove(key);
    _latestRequested.remove(key);
    await _inner.clear(key);
  }
}
