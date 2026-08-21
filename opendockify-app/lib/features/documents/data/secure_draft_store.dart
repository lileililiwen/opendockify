import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../../core/models/document.dart';
import '../../../core/storage/draft_store.dart';

/// Draft storage backed by the same platform Keychain/Keystore abstraction used
/// for authentication secrets. Each payload is scoped by its caller-provided key.
class SecureDraftStore implements DraftStore {
  SecureDraftStore([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  @override
  Future<LocalDocumentDraft?> read(String key) async {
    final raw = await _storage.read(key: key);
    if (raw == null || raw.isEmpty) return null;
    try {
      final decoded = jsonDecode(raw);
      return decoded is Map<String, dynamic>
          ? LocalDocumentDraft.fromJson(decoded)
          : null;
    } on FormatException {
      await clear(key);
      return null;
    }
  }

  @override
  Future<void> write(String key, LocalDocumentDraft draft) {
    return _storage.write(key: key, value: jsonEncode(draft.toJson()));
  }

  @override
  Future<void> clear(String key) => _storage.delete(key: key);
}
