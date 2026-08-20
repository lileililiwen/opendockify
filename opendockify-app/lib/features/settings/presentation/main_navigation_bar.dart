import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../auth/application/session_controller.dart';

/// Bottom navigation shared by the main tab screens.
class MainNavigationBar extends ConsumerWidget {
  const MainNavigationBar({super.key, required this.selectedIndex});

  final int selectedIndex;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isAdmin = ref.watch(sessionControllerProvider.select((s) => s.isAdmin));
    return NavigationBar(
      selectedIndex: selectedIndex,
      onDestinationSelected: (index) {
        switch (index) {
          case 0:
            context.go('/templates');
          case 1:
            context.go('/documents');
          case 2:
            context.go('/settings');
          case 3:
            context.go('/admin');
        }
      },
      destinations: [
        const NavigationDestination(icon: Icon(Icons.description_outlined), label: 'Templates'),
        const NavigationDestination(icon: Icon(Icons.folder_open_outlined), label: 'Documents'),
        const NavigationDestination(icon: Icon(Icons.settings_outlined), label: 'Settings'),
        if (isAdmin) const NavigationDestination(icon: Icon(Icons.admin_panel_settings_outlined), label: 'Admin'),
      ],
    );
  }
}