import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

/// Entry point for the administrator-only section.
class AdminHomeScreen extends StatelessWidget {
  const AdminHomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Admin')),
      body: ListView(
        children: [
          ListTile(
            leading: const Icon(Icons.settings_outlined),
            title: const Text('System settings'),
            subtitle: const Text('AI, LPR, and other global configuration'),
            onTap: () => context.push('/admin/settings'),
          ),
          ListTile(
            leading: const Icon(Icons.description_outlined),
            title: const Text('Global templates'),
            subtitle: const Text('Create or update public templates'),
            onTap: () => context.push('/admin/templates/new'),
          ),
          ListTile(
            leading: const Icon(Icons.history),
            title: const Text('AI usage log'),
            subtitle: const Text('Recent AI assist requests'),
            onTap: () => context.push('/admin/ai-usage'),
          ),
        ],
      ),
    );
  }
}