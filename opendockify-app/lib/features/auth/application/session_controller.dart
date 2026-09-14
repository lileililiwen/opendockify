import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/errors/error_presenter.dart';
import '../../../core/models/auth.dart';
import '../../../core/providers.dart';

enum SessionStatus { unknown, unauthenticated, authenticated }

class SessionState {
  const SessionState({
    required this.status,
    this.profile,
    this.token,
    this.notice,
    this.pendingChallengeId,
    this.pendingUsername,
  });

  final SessionStatus status;
  final UserProfile? profile;
  final String? token;
  final String? notice;

  /// 2FA challenge awaiting verification (login returned `2fa-required`).
  final String? pendingChallengeId;
  final String? pendingUsername;

  bool get isAuthenticated => status == SessionStatus.authenticated;
  bool get isAdmin => profile?.isAdministrator ?? false;
  bool get needsTwoFactor => pendingChallengeId != null && pendingChallengeId!.isNotEmpty;

  SessionState copyWith({
    SessionStatus? status,
    UserProfile? profile,
    String? token,
    String? notice,
    String? pendingChallengeId,
    String? pendingUsername,
    bool clearNotice = false,
    bool clearPending = false,
  }) =>
      SessionState(
        status: status ?? this.status,
        profile: profile ?? this.profile,
        token: token ?? this.token,
        notice: clearNotice ? null : notice ?? this.notice,
        pendingChallengeId: clearPending ? null : pendingChallengeId ?? this.pendingChallengeId,
        pendingUsername: clearPending ? null : pendingUsername ?? this.pendingUsername,
      );
}

/// Owns the auth session: token persistence (access + refresh), profile
/// loading, login/register, logout with server revocation, silent refresh on
/// 401, change-password, recovery, and 2FA verification.
class SessionController extends Notifier<SessionState> {
  @override
  SessionState build() {
    final client = ref.watch(apiClientProvider);
    client.onUnauthorized = handleUnauthorized;
    client.onRefreshNeeded = attemptRefresh;
    return const SessionState(status: SessionStatus.unknown);
  }

  /// Loads a persisted session at startup. An expired access token is
  /// refreshed silently via the 401-retry hook during [me].
  Future<void> init() async {
    final store = ref.read(tokenStoreProvider);
    final token = await store.read();
    if (token == null || token.isEmpty) {
      state = const SessionState(status: SessionStatus.unauthenticated);
      return;
    }
    final tokens = ref.read(tokenProvider);
    tokens.token = token;
    tokens.refreshToken = await store.readRefresh();
    state = SessionState(status: SessionStatus.unknown, token: token);
    try {
      final profile = await ref.read(apiClientProvider).me();
      state = SessionState(status: SessionStatus.authenticated, profile: profile, token: token);
    } on ApiError {
      await _clearAll();
      state = const SessionState(
        status: SessionStatus.unauthenticated,
        notice: 'Session expired. Please log in again.',
      );
    }
  }

  Future<String?> login(String username, String password) async {
    final client = ref.read(apiClientProvider);
    try {
      final auth = await client.login(username, password);
      if (auth.requiresTwoFactor) {
        state = SessionState(
          status: SessionStatus.unauthenticated,
          pendingChallengeId: auth.challengeId,
          pendingUsername: auth.username.isEmpty ? username : auth.username,
        );
        return null;
      }
      await _completeSignIn(auth);
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  /// Verifies the pending 2FA challenge and completes sign-in.
  Future<String?> verifyTwoFactor(String code) async {
    final challengeId = state.pendingChallengeId;
    if (challengeId == null || challengeId.isEmpty) {
      return 'No verification is pending. Please log in again.';
    }
    try {
      final auth = await ref.read(apiClientProvider).twoFactorVerify(challengeId, code.trim());
      await _completeSignIn(auth);
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  void cancelTwoFactor() {
    state = const SessionState(status: SessionStatus.unauthenticated);
  }

  Future<String?> register(String username, String password, String? displayName) async {
    final client = ref.read(apiClientProvider);
    try {
      final auth = await client.register(username, password, displayName);
      await _completeSignIn(auth);
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  /// Silent refresh for the API client's 401-retry hook. Returns true when a
  /// new access token was installed.
  Future<bool> attemptRefresh() async {
    final client = ref.read(apiClientProvider);
    final store = ref.read(tokenStoreProvider);
    final handle = ref.read(tokenProvider).refreshToken ?? await store.readRefresh();
    if (handle == null || handle.isEmpty) return false;
    try {
      final auth = await client.refresh(handle);
      if (auth.token.isEmpty) return false;
      final tokens = ref.read(tokenProvider);
      tokens.token = auth.token;
      if (auth.refreshToken != null && auth.refreshToken!.isNotEmpty) {
        tokens.refreshToken = auth.refreshToken;
        await store.writeRefresh(auth.refreshToken!);
      }
      await store.write(auth.token);
      state = state.copyWith(token: auth.token);
      return true;
    } on ApiError {
      return false;
    }
  }

  Future<String?> changePassword(String currentPassword, String newPassword) async {
    try {
      await ref.read(apiClientProvider).changePassword(currentPassword, newPassword);
      // The server revoked every session family; our stored refresh handle is
      // dead but the access token works until expiry. Rotate proactively by
      // re-logging the session state through a refresh attempt is skipped:
      // the next 401 will sign out cleanly. Keep it simple and stay signed in.
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  Future<String?> recoveryStart(String username) async {
    try {
      await ref.read(apiClientProvider).recoveryStart(username.trim());
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  Future<String?> recoveryComplete(String challengeId, String code, String newPassword) async {
    try {
      await ref.read(apiClientProvider).recoveryComplete(challengeId.trim(), code.trim(), newPassword);
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  Future<void> logout() async {
    final tokens = ref.read(tokenProvider);
    await ref.read(apiClientProvider).logout(tokens.refreshToken);
    await _clearAll();
    state = const SessionState(status: SessionStatus.unauthenticated);
  }

  void clearNotice() {
    if (state.notice != null) {
      state = state.copyWith(clearNotice: true);
    }
  }

  void handleUnauthorized() {
    ref.read(tokenStoreProvider).clear();
    final tokens = ref.read(tokenProvider);
    tokens.token = null;
    tokens.refreshToken = null;
    state = const SessionState(
      status: SessionStatus.unauthenticated,
      notice: 'Session expired. Please log in again.',
    );
  }

  Future<void> _completeSignIn(AuthResponse auth) async {
    final client = ref.read(apiClientProvider);
    final store = ref.read(tokenStoreProvider);
    final tokens = ref.read(tokenProvider);
    tokens.token = auth.token;
    tokens.refreshToken = auth.refreshToken;
    await store.write(auth.token);
    if (auth.refreshToken != null && auth.refreshToken!.isNotEmpty) {
      await store.writeRefresh(auth.refreshToken!);
    }
    final profile = await client.me();
    state = SessionState(
      status: SessionStatus.authenticated,
      profile: profile,
      token: auth.token,
    );
  }

  Future<void> _clearAll() async {
    await ref.read(tokenStoreProvider).clear();
    final tokens = ref.read(tokenProvider);
    tokens.token = null;
    tokens.refreshToken = null;
  }
}

final sessionControllerProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);
