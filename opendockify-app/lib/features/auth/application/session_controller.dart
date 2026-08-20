import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/errors/error_presenter.dart';
import '../../../core/models/auth.dart';
import '../../../core/providers.dart';

enum SessionStatus { unknown, unauthenticated, authenticated }

class SessionState {
  const SessionState({required this.status, this.profile, this.token, this.notice});

  final SessionStatus status;
  final UserProfile? profile;
  final String? token;
  final String? notice;

  bool get isAuthenticated => status == SessionStatus.authenticated;
  bool get isAdmin => profile?.isAdministrator ?? false;

  SessionState copyWith({SessionStatus? status, UserProfile? profile, String? token, String? notice, bool clearNotice = false}) =>
      SessionState(
        status: status ?? this.status,
        profile: profile ?? this.profile,
        token: token ?? this.token,
        notice: clearNotice ? null : notice ?? this.notice,
      );
}

/// Owns the auth session: token persistence, profile loading, login/register,
/// logout, and 401 recovery.
class SessionController extends Notifier<SessionState> {
  @override
  SessionState build() {
    final client = ref.watch(apiClientProvider);
    client.onUnauthorized = handleUnauthorized;
    return const SessionState(status: SessionStatus.unknown);
  }

  /// Loads a persisted session at startup.
  Future<void> init() async {
    final store = ref.read(tokenStoreProvider);
    final token = await store.read();
    if (token == null || token.isEmpty) {
      state = const SessionState(status: SessionStatus.unauthenticated);
      return;
    }
    ref.read(tokenProvider).token = token;
    state = SessionState(status: SessionStatus.unknown, token: token);
    try {
      final profile = await ref.read(apiClientProvider).me();
      state = SessionState(status: SessionStatus.authenticated, profile: profile, token: token);
    } on ApiError {
      await store.clear();
      ref.read(tokenProvider).token = null;
      state = const SessionState(
        status: SessionStatus.unauthenticated,
        notice: 'Session expired. Please log in again.',
      );
    }
  }

  Future<String?> login(String username, String password) async {
    final client = ref.read(apiClientProvider);
    final store = ref.read(tokenStoreProvider);
    try {
      final auth = await client.login(username, password);
      ref.read(tokenProvider).token = auth.token;
      await store.write(auth.token);
      final profile = await client.me();
      state = SessionState(status: SessionStatus.authenticated, profile: profile, token: auth.token);
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  Future<String?> register(String username, String password, String? displayName) async {
    final client = ref.read(apiClientProvider);
    final store = ref.read(tokenStoreProvider);
    try {
      final auth = await client.register(username, password, displayName);
      ref.read(tokenProvider).token = auth.token;
      await store.write(auth.token);
      final profile = await client.me();
      state = SessionState(status: SessionStatus.authenticated, profile: profile, token: auth.token);
      return null;
    } on ApiError catch (e) {
      return friendlyErrorMessage(e);
    }
  }

  Future<void> logout() async {
    await ref.read(tokenStoreProvider).clear();
    ref.read(tokenProvider).token = null;
    state = const SessionState(status: SessionStatus.unauthenticated);
  }

  void clearNotice() {
    if (state.notice != null) {
      state = state.copyWith(clearNotice: true);
    }
  }

  void handleUnauthorized() {
    ref.read(tokenStoreProvider).clear();
    ref.read(tokenProvider).token = null;
    state = const SessionState(
      status: SessionStatus.unauthenticated,
      notice: 'Session expired. Please log in again.',
    );
  }
}

final sessionControllerProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);