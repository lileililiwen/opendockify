import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/widgets/common.dart';
import '../application/admin_controllers.dart';

/// Lists allowlisted system settings; secret values are masked. Administrators
/// can update non-secret settings inline.
class AdminSettingsScreen extends ConsumerWidget {
  const AdminSettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final settings = ref.watch(adminSettingsControllerProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('System settings')),
      body: settings.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.read(adminSettingsControllerProvider.notifier).refresh(),
        ),
        data: (items) {
          if (items.isEmpty) {
            return const Center(child: Text('No settings available.'));
          }
          return RefreshIndicator(
            onRefresh: () => ref.read(adminSettingsControllerProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: items.length,
              itemBuilder: (context, index) {
                final setting = items[index];
                return ListTile(
                  title: Text(setting.key),
                  subtitle: Text(setting.masked ? '(masked — replace to change)' : (setting.value ?? '—')),
                  trailing: Text(setting.source, style: Theme.of(context).textTheme.bodySmall),
                  onTap: () => _editSetting(context, ref, setting.key, setting.value),
                );
              },
            ),
          );
        },
      ),
    );
  }

  Future<void> _editSetting(BuildContext context, WidgetRef ref, String key, String? currentValue) async {
    final controller = TextEditingController(text: currentValue ?? '');
    final messenger = ScaffoldMessenger.of(context);
    final result = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(key),
        content: TextField(
          controller: controller,
          decoration: InputDecoration(labelText: currentValue == null ? 'New value' : 'Value'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(context).pop(controller.text), child: const Text('Save')),
        ],
      ),
    );
    controller.dispose();
    if (result == null) return;

    final error = await ref.read(adminSettingsControllerProvider.notifier).updateSetting(key, result);
    if (!context.mounted) return;
    if (error != null) {
      messenger.showSnackBar(SnackBar(content: Text(error)));
    } else {
      messenger.showSnackBar(SnackBar(content: Text('$key updated.')));
    }
  }
}