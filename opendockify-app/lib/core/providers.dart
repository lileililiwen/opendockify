import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api/api_client.dart';
import 'config/app_config.dart';
import 'storage/token_store.dart';
import 'storage/draft_store.dart';

/// In-memory JWT holder shared by the API client and the session controller.
final tokenProvider = Provider<TokenProvider>((ref) => TokenProvider());

/// Secure storage (Keychain / Keystore) for the JWT. Overridden in tests.
final tokenStoreProvider = Provider<TokenStore>(
  (ref) => throw UnimplementedError(
    'tokenStoreProvider must be overridden in tests or wired at startup.',
  ),
);

/// Persistent preferences (base URL, disclaimer flag). Overridden in tests.
final appConfigStoreProvider = Provider<AppConfigStore>(
  (ref) => throw UnimplementedError(
    'appConfigStoreProvider must be overridden in tests or wired at startup.',
  ),
);

/// Encrypted local storage for user-scoped in-progress document drafts.
final draftStoreProvider = Provider<DraftStore>(
  (ref) => throw UnimplementedError(
    'draftStoreProvider must be overridden in tests or wired at startup.',
  ),
);

/// The single typed API client. The connection controller sets the base URL;
/// the session controller wires the 401 callback.
final apiClientProvider = Provider<ApiClient>((ref) {
  return ApiClient(baseUrl: '', tokens: ref.watch(tokenProvider));
});
