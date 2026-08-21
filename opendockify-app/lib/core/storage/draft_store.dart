import '../models/document.dart';

/// User/form-scoped storage for sensitive in-progress document snapshots.
abstract class DraftStore {
  Future<LocalDocumentDraft?> read(String key);
  Future<void> write(String key, LocalDocumentDraft draft);
  Future<void> clear(String key);
}
