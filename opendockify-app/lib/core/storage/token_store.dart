/// Storage for the access JWT and the opaque refresh handle. The real
/// implementation uses the platform secure storage (Keychain / Keystore).
/// Tests inject an in-memory fake.
abstract class TokenStore {
  Future<String?> read();
  Future<void> write(String token);
  Future<String?> readRefresh();
  Future<void> writeRefresh(String refreshToken);
  Future<void> clear();
}
