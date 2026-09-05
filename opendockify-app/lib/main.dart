import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core/providers.dart';
import 'core/storage/draft_store.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/auth/data/secure_token_store.dart';
import 'features/documents/data/secure_draft_store.dart';
import 'core/config/app_config.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final container = ProviderContainer(
    overrides: [
      tokenStoreProvider.overrideWithValue(SecureTokenStore()),
      appConfigStoreProvider.overrideWithValue(
        AppConfigStore(SharedPreferencesAsync()),
      ),
      draftStoreProvider.overrideWithValue(
        RevisionedDraftStore(SecureDraftStore()),
      ),
    ],
  );
  runApp(
    UncontrolledProviderScope(
      container: container,
      child: const OpenDockifyApp(),
    ),
  );
}

class OpenDockifyApp extends ConsumerWidget {
  const OpenDockifyApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(routerProvider);
    return MaterialApp.router(
      title: 'OpenDockify',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      routerConfig: router,
    );
  }
}
