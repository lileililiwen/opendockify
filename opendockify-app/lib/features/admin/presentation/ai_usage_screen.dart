import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/widgets/common.dart';
import '../application/admin_controllers.dart';

/// Administrator view of recent AI usage entries.
class AiUsageScreen extends ConsumerWidget {
  const AiUsageScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final usage = ref.watch(aiUsageControllerProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('AI usage')),
      body: usage.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.read(aiUsageControllerProvider.notifier).refresh(),
        ),
        data: (entries) {
          if (entries.isEmpty) {
            return const Center(child: Text('No AI usage recorded.'));
          }
          return RefreshIndicator(
            onRefresh: () => ref.read(aiUsageControllerProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: entries.length,
              itemBuilder: (context, index) {
                final entry = entries[index];
                final time = entry.timestamp == null
                    ? '—'
                    : '${entry.timestamp!.year}-${entry.timestamp!.month.toString().padLeft(2, '0')}-${entry.timestamp!.day.toString().padLeft(2, '0')} '
                        '${entry.timestamp!.hour.toString().padLeft(2, '0')}:${entry.timestamp!.minute.toString().padLeft(2, '0')}';
                return ExpansionTile(
                  title: Text(entry.action ?? 'polish'),
                  subtitle: Text('$time · user ${entry.userId ?? '?'} · ${entry.success ? 'ok' : 'failed'}'),
                  children: [
                    if (entry.requestSnippet != null && entry.requestSnippet!.isNotEmpty)
                      ListTile(title: const Text('Request'), subtitle: Text(entry.requestSnippet!)),
                    if (entry.responseSnippet != null && entry.responseSnippet!.isNotEmpty)
                      ListTile(title: const Text('Response'), subtitle: Text(entry.responseSnippet!)),
                  ],
                );
              },
            ),
          );
        },
      ),
    );
  }
}