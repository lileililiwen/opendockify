import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../../core/legal/legal_text.dart';
import '../../../core/config/connection_controller.dart';
import '../../auth/application/session_controller.dart';
import 'main_navigation_bar.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider);
    final connection = ref.watch(connectionControllerProvider);
    final profile = session.profile;
    final isAdmin = session.isAdmin;

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        children: [
          if (profile != null)
            ListTile(
              leading: const Icon(Icons.account_circle),
              title: Text(profile.displayName.isEmpty ? profile.username : profile.displayName),
              subtitle: Text('@${profile.username} · ${profile.role}'),
            ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.dns_outlined),
            title: const Text('Server'),
            subtitle: Text(connection.baseUrl ?? 'Not configured'),
            onTap: () => context.push('/connect'),
          ),
          ListTile(
            leading: const Icon(Icons.hub_outlined),
            title: const Text('Integrations'),
            subtitle: const Text('Service tokens and webhooks'),
            onTap: () => context.push('/integrations'),
          ),
          ListTile(
            leading: const Icon(Icons.password_outlined),
            title: const Text('Change password'),
            onTap: () => context.push('/change-password'),
          ),
          const NotificationPrefsTile(),
          ListTile(
            leading: const Icon(Icons.info_outline),
            title: const Text('About & Legal'),
            onTap: () => _showAbout(context),
          ),
          if (isAdmin) ...[
            const Divider(),
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
              child: Text('Administrator', style: TextStyle(fontWeight: FontWeight.bold)),
            ),
            ListTile(
              leading: const Icon(Icons.admin_panel_settings_outlined),
              title: const Text('Admin'),
              onTap: () => context.push('/admin'),
            ),
          ],
          const Divider(),
          ListTile(
            leading: const Icon(Icons.logout),
            title: const Text('Sign out'),
            onTap: () async {
              await ref.read(sessionControllerProvider.notifier).logout();
              if (context.mounted) context.go('/login');
            },
          ),
        ],
      ),
      bottomNavigationBar: const MainNavigationBar(selectedIndex: 2),
    );
  }

  void _showAbout(BuildContext context) {
    showAboutDialog(
      context: context,
      applicationName: 'OpenDockify',
      applicationVersion: '1.0.0',
      applicationLegalese: aboutScreenText,
      children: const [
        SizedBox(height: 8),
        Text('This is a document drafting tool only, not legal advice.'),
      ],
    );
  }
}

class NotificationPrefsTile extends ConsumerStatefulWidget {
  const NotificationPrefsTile({super.key});

  @override
  ConsumerState<NotificationPrefsTile> createState() =>
      _NotificationPrefsTileState();
}

class _NotificationPrefsTileState
    extends ConsumerState<NotificationPrefsTile> {
  static const _key = 'notify_prefs_enabled';
  bool? _enabled;

  @override
  void initState() {
    super.initState();
    SharedPreferences.getInstance().then((prefs) {
      if (mounted) setState(() => _enabled = prefs.getBool(_key) ?? false);
    });
  }

  @override
  Widget build(BuildContext context) {
    return SwitchListTile(
      secondary: const Icon(Icons.notifications_outlined),
      title: const Text('Email notifications'),
      subtitle: const Text('Share, expiry and finalize alerts'),
      value: _enabled ?? false,
      onChanged: (value) async {
        final prefs = await SharedPreferences.getInstance();
        await prefs.setBool(_key, value);
        if (mounted) setState(() => _enabled = value);
      },
    );
  }
}