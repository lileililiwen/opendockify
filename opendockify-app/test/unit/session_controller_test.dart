import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/api/api_client.dart';
import 'package:opendockify_app/core/api/api_error.dart';
import 'package:opendockify_app/core/models/auth.dart';
import 'package:opendockify_app/core/providers.dart';
import 'package:opendockify_app/core/storage/token_store.dart';
import 'package:opendockify_app/features/auth/application/session_controller.dart';

class FakeTokenStore implements TokenStore {
  String? stored;

  @override
  Future<String?> read() async => stored;

  @override
  Future<void> write(String token) async => stored = token;

  @override
  Future<void> clear() async => stored = null;
}

class FakeApiClient extends ApiClient {
  FakeApiClient({super.baseUrl = 'http://test', required super.tokens});

  bool failLogin = false;
  String? lastLoginUser;
  String? lastLoginPassword;
  AuthResponse? registerResponse;

  @override
  Future<AuthResponse> login(String username, String password) async {
    lastLoginUser = username;
    lastLoginPassword = password;
    if (failLogin) {
      throw const ApiError(ApiErrorKind.validation, 'Invalid credentials.');
    }
    return AuthResponse(id: 'u1', username: username, role: 'User', token: 'jwt');
  }

  @override
  Future<UserProfile> me() async {
    if (failLogin) {
      throw const ApiError(ApiErrorKind.unauthorized, 'Session expired.');
    }
    return const UserProfile(
      id: 'u1',
      username: 'alice',
      displayName: 'Alice',
      role: 'User',
      isAdministrator: false,
    );
  }
}

void main() {
  late ProviderContainer container;
  late FakeTokenStore store;
  late FakeApiClient client;

  setUp(() {
    store = FakeTokenStore();
    client = FakeApiClient(tokens: TokenProvider());
    container = ProviderContainer(
      overrides: [
        apiClientProvider.overrideWithValue(client),
        tokenStoreProvider.overrideWithValue(store),
      ],
    );
    addTearDown(container.dispose);
  });

  group('SessionController', () {
    test('starts in unknown state', () {
      expect(container.read(sessionControllerProvider).status, SessionStatus.unknown);
    });

    test('init without a token goes unauthenticated', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.init();
      expect(container.read(sessionControllerProvider).status, SessionStatus.unauthenticated);
    });

    test('login persists token and loads profile', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      final error = await controller.login('alice', 'pw');
      expect(error, isNull);
      expect(store.stored, 'jwt');
      expect(client.lastLoginUser, 'alice');
      final session = container.read(sessionControllerProvider);
      expect(session.isAuthenticated, isTrue);
      expect(session.profile?.username, 'alice');
      expect(container.read(tokenProvider).token, 'jwt');
    });

    test('login failure surfaces message and does not persist', () async {
      client.failLogin = true;
      final controller = container.read(sessionControllerProvider.notifier);
      final error = await controller.login('alice', 'wrong');
      expect(error, isNotEmpty);
      expect(store.stored, isNull);
      expect(container.read(sessionControllerProvider).isAuthenticated, isFalse);
    });

    test('init restores a persisted session', () async {
      store.stored = 'jwt';
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.init();
      final session = container.read(sessionControllerProvider);
      expect(session.isAuthenticated, isTrue);
      expect(session.token, 'jwt');
    });

    test('init clears an invalid persisted session', () async {
      client.failLogin = true;
      store.stored = 'expired';
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.init();
      final session = container.read(sessionControllerProvider);
      expect(session.isAuthenticated, isFalse);
      expect(store.stored, isNull);
      expect(session.notice, isNotNull);
    });

    test('logout clears token and store', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.login('alice', 'pw');
      await controller.logout();
      expect(store.stored, isNull);
      expect(container.read(tokenProvider).token, isNull);
      expect(container.read(sessionControllerProvider).isAuthenticated, isFalse);
    });
  });
}