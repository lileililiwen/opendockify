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
  String? storedRefresh;

  @override
  Future<String?> read() async => stored;

  @override
  Future<void> write(String token) async => stored = token;

  @override
  Future<String?> readRefresh() async => storedRefresh;

  @override
  Future<void> writeRefresh(String refreshToken) async => storedRefresh = refreshToken;

  @override
  Future<void> clear() async {
    stored = null;
    storedRefresh = null;
  }
}

class FakeApiClient extends ApiClient {
  FakeApiClient({super.baseUrl = 'http://test', required super.tokens});

  bool failLogin = false;
  bool failRefresh = false;
  bool twoFactorRequired = false;
  String? lastLoginUser;
  String? lastLoginPassword;
  String? lastRefreshHandle;
  String? lastLogoutHandle;
  AuthResponse? registerResponse;

  @override
  Future<AuthResponse> login(String username, String password) async {
    lastLoginUser = username;
    lastLoginPassword = password;
    if (failLogin) {
      throw const ApiError(ApiErrorKind.validation, 'Invalid credentials.');
    }
    if (twoFactorRequired) {
      return const AuthResponse(
        id: 'u1',
        username: 'alice',
        role: 'User',
        token: '',
        mode: '2fa-required',
        challengeId: 'challenge-1',
      );
    }
    return const AuthResponse(
      id: 'u1',
      username: 'alice',
      role: 'User',
      token: 'jwt',
      refreshToken: 'refresh-1',
    );
  }

  @override
  Future<AuthResponse> refresh(String refreshToken) async {
    lastRefreshHandle = refreshToken;
    if (failRefresh) {
      throw const ApiError(ApiErrorKind.unauthorized, 'Refresh token rejected.');
    }
    return const AuthResponse(
      id: 'u1',
      username: 'alice',
      role: 'User',
      token: 'jwt-2',
      refreshToken: 'refresh-2',
    );
  }

  @override
  Future<void> logout(String? refreshToken) async {
    lastLogoutHandle = refreshToken;
  }

  @override
  Future<AuthResponse> twoFactorVerify(String challengeId, String code) async {
    if (code != '123456') {
      throw const ApiError(ApiErrorKind.unauthorized, 'Two-factor verification failed.');
    }
    return const AuthResponse(
      id: 'u1',
      username: 'alice',
      role: 'User',
      token: 'jwt',
      refreshToken: 'refresh-1',
    );
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

    test('login persists the refresh handle', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.login('alice', 'pw');
      expect(store.storedRefresh, 'refresh-1');
      expect(container.read(tokenProvider).refreshToken, 'refresh-1');
    });

    test('logout revokes the refresh handle server-side', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.login('alice', 'pw');
      await controller.logout();
      expect(client.lastLogoutHandle, 'refresh-1');
      expect(store.storedRefresh, isNull);
    });

    test('attemptRefresh rotates tokens', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.login('alice', 'pw');
      final ok = await controller.attemptRefresh();
      expect(ok, isTrue);
      expect(client.lastRefreshHandle, 'refresh-1');
      expect(store.stored, 'jwt-2');
      expect(store.storedRefresh, 'refresh-2');
    });

    test('attemptRefresh without a handle returns false', () async {
      final controller = container.read(sessionControllerProvider.notifier);
      expect(await controller.attemptRefresh(), isFalse);
      expect(client.lastRefreshHandle, isNull);
    });

    test('login with 2FA required parks a pending challenge', () async {
      client.twoFactorRequired = true;
      final controller = container.read(sessionControllerProvider.notifier);
      final error = await controller.login('alice', 'pw');
      expect(error, isNull);
      final session = container.read(sessionControllerProvider);
      expect(session.isAuthenticated, isFalse);
      expect(session.needsTwoFactor, isTrue);
      expect(session.pendingChallengeId, 'challenge-1');
    });

    test('verifyTwoFactor completes sign-in', () async {
      client.twoFactorRequired = true;
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.login('alice', 'pw');
      final error = await controller.verifyTwoFactor('123456');
      expect(error, isNull);
      expect(container.read(sessionControllerProvider).isAuthenticated, isTrue);
    });

    test('verifyTwoFactor surfaces a bad code', () async {
      client.twoFactorRequired = true;
      final controller = container.read(sessionControllerProvider.notifier);
      await controller.login('alice', 'pw');
      final error = await controller.verifyTwoFactor('000000');
      expect(error, isNotEmpty);
      expect(container.read(sessionControllerProvider).isAuthenticated, isFalse);
    });
  });
}