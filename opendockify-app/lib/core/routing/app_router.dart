import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../config/connection_controller.dart';
import '../../features/admin/presentation/admin_home_screen.dart';
import '../../features/admin/presentation/admin_settings_screen.dart';
import '../../features/admin/presentation/admin_template_upsert_screen.dart';
import '../../features/admin/presentation/template_package_import_screen.dart';
import '../../features/admin/presentation/ai_usage_screen.dart';
import '../../features/integrations/presentation/integrations_screen.dart';
import '../../features/auth/application/session_controller.dart';
import '../../features/auth/presentation/change_password_screen.dart';
import '../../features/auth/presentation/connection_screen.dart';
import '../../features/auth/presentation/login_screen.dart';
import '../../features/auth/presentation/recovery_screen.dart';
import '../../features/auth/presentation/register_screen.dart';
import '../../features/auth/presentation/splash_screen.dart';
import '../../features/auth/presentation/two_factor_verify_screen.dart';
import '../../features/documents/presentation/document_detail_screen.dart';
import '../../features/documents/presentation/document_fill_screen.dart';
import '../../features/documents/presentation/documents_screen.dart';
import '../../features/templates/presentation/marketplace_screen.dart';
import '../../features/templates/presentation/template_detail_screen.dart';
import '../../features/templates/presentation/template_editor_screen.dart';
import '../../features/settings/presentation/settings_screen.dart';

class RouterRefresh extends ChangeNotifier {
  void notify() => notifyListeners();
}

final routerProvider = Provider<GoRouter>((ref) {
  final refresh = RouterRefresh();
  final sub1 = ref.listen(connectionControllerProvider, (_, _) => refresh.notify());
  final sub2 = ref.listen(sessionControllerProvider, (_, _) => refresh.notify());
  ref.onDispose(() {
    sub1.close();
    sub2.close();
  });

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: refresh,
    redirect: (context, state) {
      final connection = ref.read(connectionControllerProvider);
      final session = ref.read(sessionControllerProvider);
      final location = state.matchedLocation;

      final onSplash = location == '/splash';
      final onConnect = location == '/connect';
      final onAuth = location == '/login' ||
          location == '/register' ||
          location == '/recovery' ||
          location == '/2fa-verify';

      // Let the splash screen finish bootstrapping before redirecting.
      if (onSplash && (connection.isLoading || session.status == SessionStatus.unknown)) {
        return null;
      }

      if (connection.isLoading && location == '/connect') return null;

      if (!connection.isConfigured) {
        return onConnect ? null : '/connect';
      }

      if (session.status == SessionStatus.unknown) {
        return onSplash ? null : '/splash';
      }

      final authenticated = session.isAuthenticated;
      if (!authenticated) {
        return onAuth ? null : '/login';
      }

      if (onAuth) return '/templates';

      final requiresAdmin = location.startsWith('/admin');
      if (requiresAdmin && !session.isAdmin) {
        return '/templates';
      }

      return null;
    },
    routes: [
      GoRoute(path: '/splash', builder: (context, state) => const SplashScreen()),
      GoRoute(path: '/connect', builder: (context, state) => const ConnectionScreen()),
      GoRoute(path: '/login', builder: (context, state) => const LoginScreen()),
      GoRoute(path: '/register', builder: (context, state) => const RegisterScreen()),
      GoRoute(path: '/recovery', builder: (context, state) => const RecoveryScreen()),
      GoRoute(path: '/2fa-verify', builder: (context, state) => const TwoFactorVerifyScreen()),
      GoRoute(path: '/change-password', builder: (context, state) => const ChangePasswordScreen()),
      GoRoute(path: '/templates', builder: (context, state) => const MarketplaceScreen()),
      GoRoute(path: '/templates/new', builder: (context, state) => const TemplateEditorScreen()),
      GoRoute(path: '/templates/:id', builder: (context, state) => TemplateDetailScreen(templateId: state.pathParameters['id']!)),
      GoRoute(path: '/templates/:id/edit', builder: (context, state) => TemplateEditorScreen(templateId: state.pathParameters['id'])),
      GoRoute(path: '/templates/:id/fill', builder: (context, state) => DocumentFillScreen(templateId: state.pathParameters['id']!)),
      GoRoute(path: '/documents', builder: (context, state) => const DocumentsScreen()),
      GoRoute(path: '/documents/:id', builder: (context, state) => DocumentDetailScreen(documentId: state.pathParameters['id']!)),
      GoRoute(path: '/documents/:id/reedit', builder: (context, state) => DocumentFillScreen(documentId: state.pathParameters['id']!)),
      GoRoute(path: '/admin', builder: (context, state) => const AdminHomeScreen()),
      GoRoute(path: '/integrations', builder: (context, state) => const IntegrationsScreen()),
      GoRoute(path: '/admin/settings', builder: (context, state) => const AdminSettingsScreen()),
      GoRoute(path: '/admin/templates/new', builder: (context, state) => const AdminTemplateUpsertScreen()),
      GoRoute(path: '/admin/templates/import', builder: (context, state) => const TemplatePackageImportScreen()),
      GoRoute(path: '/admin/ai-usage', builder: (context, state) => const AiUsageScreen()),
      GoRoute(path: '/settings', builder: (context, state) => const SettingsScreen()),
    ],
  );
});
