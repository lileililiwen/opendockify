import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/api/api_client.dart';
import 'package:opendockify_app/core/config/app_config.dart';
import 'package:opendockify_app/core/config/connection_controller.dart';
import 'package:opendockify_app/core/providers.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:shared_preferences_platform_interface/in_memory_shared_preferences_async.dart';
import 'package:shared_preferences_platform_interface/shared_preferences_async_platform_interface.dart';

class FakeAppConfigStore extends AppConfigStore {
  FakeAppConfigStore() : super(SharedPreferencesAsync());

  String? baseUrl;

  @override
  Future<String?> readBaseUrl() async => baseUrl;

  @override
  Future<void> writeBaseUrl(String url) async => baseUrl = url;

  @override
  Future<void> clearBaseUrl() async => baseUrl = null;
}

class FakeApiClient extends ApiClient {
  FakeApiClient({super.baseUrl = 'http://test', required super.tokens});

  bool healthy = true;

  @override
  Future<bool> healthCheck() async => healthy;
}

void main() {
  setUpAll(() {
    SharedPreferencesAsyncPlatform.instance = InMemorySharedPreferencesAsync.empty();
  });

  late ProviderContainer container;
  late FakeAppConfigStore store;
  late FakeApiClient client;

  setUp(() {
    store = FakeAppConfigStore();
    client = FakeApiClient(tokens: TokenProvider());
    container = ProviderContainer(
      overrides: [
        appConfigStoreProvider.overrideWithValue(store),
        apiClientProvider.overrideWithValue(client),
      ],
    );
    addTearDown(container.dispose);
  });

  group('ConnectionController.normalize', () {
    test('adds http scheme when missing', () {
      expect(ConnectionController.normalize('192.168.1.10:8080'), 'http://192.168.1.10:8080');
    });

    test('keeps https scheme', () {
      expect(ConnectionController.normalize('https://dock.local'), 'https://dock.local');
    });

    test('strips trailing slashes', () {
      expect(ConnectionController.normalize('http://host:8080/'), 'http://host:8080');
    });

    test('rejects blank input', () {
      expect(ConnectionController.normalize('  '), isNull);
    });

    test('rejects unsupported schemes', () {
      expect(ConnectionController.normalize('ftp://host'), isNull);
    });
  });

  group('ConnectionController', () {
    test('init without stored url leaves unconfigured', () async {
      await container.read(connectionControllerProvider.notifier).init();
      final state = container.read(connectionControllerProvider);
      expect(state.isConfigured, isFalse);
      expect(state.isLoading, isFalse);
    });

    test('connect persists a valid url', () async {
      final controller = container.read(connectionControllerProvider.notifier);
      final error = await controller.connect('http://localhost:8080');
      expect(error, isNull);
      expect(store.baseUrl, 'http://localhost:8080');
      expect(container.read(connectionControllerProvider).isConfigured, isTrue);
      expect(client.baseUrl, 'http://localhost:8080');
    });

    test('connect rejects an unreachable server', () async {
      client.healthy = false;
      final controller = container.read(connectionControllerProvider.notifier);
      final error = await controller.connect('http://localhost:9999');
      expect(error, isNotNull);
      expect(store.baseUrl, isNull);
      expect(container.read(connectionControllerProvider).isConfigured, isFalse);
    });

    test('connect rejects invalid urls without calling server', () async {
      final controller = container.read(connectionControllerProvider.notifier);
      final error = await controller.connect('not a url');
      expect(error, isNotNull);
      expect(client.baseUrl, 'http://test');
    });

    test('init restores a persisted url', () async {
      store.baseUrl = 'http://localhost:8080';
      await container.read(connectionControllerProvider.notifier).init();
      expect(container.read(connectionControllerProvider).baseUrl, 'http://localhost:8080');
      expect(client.baseUrl, 'http://localhost:8080');
    });

    test('reset clears the stored url', () async {
      store.baseUrl = 'http://localhost:8080';
      final controller = container.read(connectionControllerProvider.notifier);
      await controller.init();
      await controller.reset();
      expect(store.baseUrl, isNull);
      expect(container.read(connectionControllerProvider).isConfigured, isFalse);
    });
  });
}