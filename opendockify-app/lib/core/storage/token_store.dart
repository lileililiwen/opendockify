/// Storage for the JWT. The real implementation uses the platform secure
/// storage (Keychain / Keystore). Tests inject an in-memory fake.
abstract class TokenStore {
  Future<String?> read();
  Future<void> write(String token);
  Future<void> clear();
}
