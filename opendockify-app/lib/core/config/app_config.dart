import 'package:shared_preferences/shared_preferences.dart';

/// Persists the self-hosted server base URL and the "disclaimer seen" flag.
class AppConfigStore {
  AppConfigStore(this._prefs);

  final SharedPreferencesAsync _prefs;

  static const String baseUrlKey = 'server_base_url';
  static const String disclaimerSeenKey = 'legal_disclaimer_seen';

  Future<String?> readBaseUrl() => _prefs.getString(baseUrlKey);

  Future<void> writeBaseUrl(String url) => _prefs.setString(baseUrlKey, url);

  Future<void> clearBaseUrl() => _prefs.remove(baseUrlKey);

  Future<bool> readDisclaimerSeen() async => await _prefs.getBool(disclaimerSeenKey) ?? false;

  Future<void> writeDisclaimerSeen() => _prefs.setBool(disclaimerSeenKey, true);
}

class ConnectionState {
  const ConnectionState({
    required this.isLoading,
    required this.baseUrl,
    this.error,
  });

  final bool isLoading;
  final String? baseUrl;
  final String? error;

  bool get isConfigured => baseUrl != null && baseUrl!.isNotEmpty;

  ConnectionState copyWith({bool? isLoading, String? baseUrl, String? error, bool clearError = false}) =>
      ConnectionState(
        isLoading: isLoading ?? this.isLoading,
        baseUrl: baseUrl ?? this.baseUrl,
        error: clearError ? null : error ?? this.error,
      );
}