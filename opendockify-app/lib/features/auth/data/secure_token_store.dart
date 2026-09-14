import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../../core/storage/token_store.dart';

/// JWT + refresh-handle storage backed by the platform secure storage
/// (Keychain / Keystore).
class SecureTokenStore implements TokenStore {
  SecureTokenStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  static const String _key = 'auth_token';
  static const String _refreshKey = 'auth_refresh_token';

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read() => _storage.read(key: _key);

  @override
  Future<void> write(String token) => _storage.write(key: _key, value: token);

  @override
  Future<String?> readRefresh() => _storage.read(key: _refreshKey);

  @override
  Future<void> writeRefresh(String refreshToken) => _storage.write(key: _refreshKey, value: refreshToken);

  @override
  Future<void> clear() async {
    await _storage.delete(key: _key);
    await _storage.delete(key: _refreshKey);
  }
}