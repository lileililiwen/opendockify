import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/config/app_config.dart';
import 'package:opendockify_app/core/providers.dart';
import 'package:opendockify_app/core/storage/token_store.dart';
import 'package:opendockify_app/main.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:shared_preferences_platform_interface/in_memory_shared_preferences_async.dart';
import 'package:shared_preferences_platform_interface/shared_preferences_async_platform_interface.dart';

class FakeTokenStore implements TokenStore {
  @override
  Future<String?> read() async => null;

  @override
  Future<void> write(String token) async {}

  @override
  Future<String?> readRefresh() async => null;

  @override
  Future<void> writeRefresh(String refreshToken) async {}

  @override
  Future<void> clear() async {}
}

class FakeAppConfigStore extends AppConfigStore {
  FakeAppConfigStore() : super(SharedPreferencesAsync());

  @override
  Future<String?> readBaseUrl() async => null;
}

void main() {
  testWidgets('app boots to the connection screen when no server is configured', (tester) async {
    SharedPreferencesAsyncPlatform.instance = InMemorySharedPreferencesAsync.empty();
    final container = ProviderContainer(
      overrides: [
        tokenStoreProvider.overrideWithValue(FakeTokenStore()),
        appConfigStoreProvider.overrideWithValue(FakeAppConfigStore()),
      ],
    );
    addTearDown(container.dispose);

    await tester.pumpWidget(UncontrolledProviderScope(container: container, child: const OpenDockifyApp()));
    // Let the splash bootstrap run (post-frame callback + async store reads),
    // then the router redirects to the connection screen.
    await tester.pump();
    await tester.pump();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.text('Connect to server'), findsOneWidget);
    expect(find.byType(TextField), findsOneWidget);
    expect(find.text('Connect'), findsOneWidget);
  });
}