import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers.dart';
import 'app_config.dart';

/// Owns the self-hosted server connection: base URL persistence, health check,
/// and reset.
class ConnectionController extends Notifier<ConnectionState> {
  @override
  ConnectionState build() => const ConnectionState(isLoading: true, baseUrl: null);

  Future<void> init() async {
    final store = ref.read(appConfigStoreProvider);
    final baseUrl = await store.readBaseUrl();
    if (baseUrl == null || baseUrl.isEmpty) {
      state = const ConnectionState(isLoading: false, baseUrl: null);
      return;
    }
    ref.read(apiClientProvider).setBaseUrl(baseUrl);
    state = ConnectionState(isLoading: false, baseUrl: baseUrl);
  }

  /// Validates and persists a server URL. Returns an error message or null on
  /// success.
  Future<String?> connect(String rawUrl) async {
    final store = ref.read(appConfigStoreProvider);
    final client = ref.read(apiClientProvider);
    final url = normalize(rawUrl);
    if (url == null) {
      return 'Enter a valid URL, e.g. http://192.168.1.10:8080';
    }
    state = state.copyWith(isLoading: true, clearError: true, baseUrl: url);
    client.setBaseUrl(url);
    final ok = await client.healthCheck();
    if (!ok) {
      state = ConnectionState(
        isLoading: false,
        baseUrl: null,
        error: 'Cannot reach server at that URL. Check the address and that the server is running.',
      );
      return 'Cannot reach server.';
    }
    await store.writeBaseUrl(url);
    state = ConnectionState(isLoading: false, baseUrl: url);
    return null;
  }

  Future<void> reset() async {
    await ref.read(appConfigStoreProvider).clearBaseUrl();
    state = const ConnectionState(isLoading: false, baseUrl: null);
  }

  static String? normalize(String raw) {
    var s = raw.trim();
    if (s.isEmpty) return null;
    if (s.contains(RegExp(r'\s'))) return null;
    final uri0 = Uri.tryParse(s);
    if (uri0 != null && uri0.hasScheme) {
      if (uri0.scheme != 'http' && uri0.scheme != 'https') return null;
      if (uri0.host.isEmpty) return null;
      return s.replaceFirst(RegExp(r'/+$'), '');
    }
    s = 'http://$s';
    final uri = Uri.tryParse(s);
    if (uri == null || !uri.isAbsolute || (uri.scheme != 'http' && uri.scheme != 'https')) return null;
    if (uri.host.isEmpty) return null;
    return s.replaceFirst(RegExp(r'/+$'), '');
  }
}

final connectionControllerProvider = NotifierProvider<ConnectionController, ConnectionState>(ConnectionController.new);