import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/widgets/common.dart';
import '../../settings/presentation/main_navigation_bar.dart';
import '../application/documents_controller.dart';

class DocumentsScreen extends ConsumerWidget {
  const DocumentsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final documents = ref.watch(documentsControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Documents'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.read(documentsControllerProvider.notifier).refresh(),
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: documents.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.read(documentsControllerProvider.notifier).refresh(),
        ),
        data: (page) {
          if (page.items.isEmpty) {
            return const Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.folder_open_outlined, size: 48),
                  SizedBox(height: 12),
                  Text('No documents yet. Generate one from a template.'),
                ],
              ),
            );
          }
          return RefreshIndicator(
            onRefresh: () => ref.read(documentsControllerProvider.notifier).refresh(),
            child: ListView.builder(
              itemCount: page.items.length + (page.hasMore ? 1 : 0),
              itemBuilder: (context, index) {
                if (index >= page.items.length) {
                  return Padding(
                    padding: const EdgeInsets.all(16),
                    child: Center(
                      child: InkWell(
                        onTap: () => ref.read(documentsControllerProvider.notifier).loadMore(),
                        child: const Text('Load more'),
                      ),
                    ),
                  );
                }
                final doc = page.items[index];
                return ListTile(
                  leading: const Icon(Icons.description_outlined),
                  title: Text(_formatDate(doc.createdAt)),
                  subtitle: Text(
                    'Status: ${doc.status}${doc.parentId != null ? ' · re-edited' : ''}',
                  ),
                  onTap: () => context.push('/documents/${doc.id}'),
                );
              },
            ),
          );
        },
      ),
      bottomNavigationBar: const MainNavigationBar(selectedIndex: 1),
    );
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    return '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')} '
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}