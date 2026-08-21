import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/widgets/common.dart';
import '../../../core/models/document.dart';
import '../../settings/presentation/main_navigation_bar.dart';
import '../application/documents_controller.dart';

class DocumentsScreen extends ConsumerStatefulWidget {
  const DocumentsScreen({super.key});

  @override
  ConsumerState<DocumentsScreen> createState() => _DocumentsScreenState();
}

class _DocumentsScreenState extends ConsumerState<DocumentsScreen> {
  final _searchController = TextEditingController();
  Timer? _searchDebounce;
  String _archive = 'active';
  String _sort = 'newest';

  @override
  void dispose() {
    _searchDebounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 350), () {
      ref.read(documentsControllerProvider.notifier).setSearch(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final documents = ref.watch(documentsControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Documents'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () =>
                ref.read(documentsControllerProvider.notifier).refresh(),
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 4),
            child: SearchBar(
              controller: _searchController,
              hintText: 'Search title or template',
              leading: const Icon(Icons.search),
              trailing: [
                if (_searchController.text.isNotEmpty)
                  IconButton(
                    onPressed: () {
                      _searchController.clear();
                      setState(() {});
                      ref
                          .read(documentsControllerProvider.notifier)
                          .setSearch('');
                    },
                    icon: const Icon(Icons.clear),
                    tooltip: 'Clear search',
                  ),
              ],
              onChanged: (value) {
                setState(() {});
                _onSearchChanged(value);
              },
              onSubmitted: (value) {
                _searchDebounce?.cancel();
                ref.read(documentsControllerProvider.notifier).setSearch(value);
              },
            ),
          ),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Row(
              children: [
                SegmentedButton<String>(
                  segments: const [
                    ButtonSegment(value: 'active', label: Text('Active')),
                    ButtonSegment(value: 'archived', label: Text('Archived')),
                    ButtonSegment(value: 'all', label: Text('All')),
                  ],
                  selected: {_archive},
                  onSelectionChanged: (selected) {
                    final value = selected.single;
                    setState(() => _archive = value);
                    ref
                        .read(documentsControllerProvider.notifier)
                        .setArchive(value);
                  },
                ),
                const SizedBox(width: 12),
                DropdownButton<String>(
                  value: _sort,
                  items: const [
                    DropdownMenuItem(value: 'newest', child: Text('Newest')),
                    DropdownMenuItem(value: 'oldest', child: Text('Oldest')),
                    DropdownMenuItem(value: 'title', child: Text('Title')),
                  ],
                  onChanged: (value) {
                    if (value == null) return;
                    setState(() => _sort = value);
                    ref
                        .read(documentsControllerProvider.notifier)
                        .setSort(value);
                  },
                ),
              ],
            ),
          ),
          const SizedBox(height: 4),
          Expanded(child: _buildResults(documents)),
        ],
      ),
      bottomNavigationBar: const MainNavigationBar(selectedIndex: 1),
    );
  }

  Widget _buildResults(AsyncValue<DocumentListPage> documents) {
    return documents.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (error, _) => ErrorView(
        message: error.toString(),
        onRetry: () => ref.read(documentsControllerProvider.notifier).refresh(),
      ),
      data: (page) {
        if (page.items.isEmpty) {
          return Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(Icons.folder_open_outlined, size: 48),
                const SizedBox(height: 12),
                Text(
                  _searchController.text.isEmpty
                      ? 'No documents in this view.'
                      : 'No documents match your search.',
                ),
              ],
            ),
          );
        }
        return RefreshIndicator(
          onRefresh: () =>
              ref.read(documentsControllerProvider.notifier).refresh(),
          child: ListView.builder(
            itemCount: page.items.length + (page.hasMore ? 1 : 0),
            itemBuilder: (context, index) {
              if (index >= page.items.length) {
                return Padding(
                  padding: const EdgeInsets.all(16),
                  child: Center(
                    child: TextButton(
                      onPressed: () => ref
                          .read(documentsControllerProvider.notifier)
                          .loadMore(),
                      child: const Text('Load more'),
                    ),
                  ),
                );
              }
              final doc = page.items[index];
              return ListTile(
                leading: Icon(
                  doc.isArchived
                      ? Icons.archive_outlined
                      : Icons.description_outlined,
                ),
                title: Text(
                  doc.title.isEmpty ? 'Untitled document' : doc.title,
                ),
                subtitle: Text(
                  '${doc.templateName}\n${_formatDate(doc.createdAt)}${doc.parentId != null ? ' · version' : ''}',
                ),
                isThreeLine: true,
                trailing: doc.isArchived
                    ? const Chip(label: Text('Archived'))
                    : null,
                onTap: () => context.push('/documents/${doc.id}'),
              );
            },
          ),
        );
      },
    );
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    return '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')} '
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}
