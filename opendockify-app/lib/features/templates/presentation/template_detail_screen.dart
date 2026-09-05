import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:share_plus/share_plus.dart';

import 'dart:typed_data';

import '../../../core/providers.dart';
import '../../../core/models/template.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/widgets/common.dart';
import '../../../core/errors/error_presenter.dart';
import '../../ai/presentation/polish_clause_button.dart';
import '../application/marketplace_controller.dart';
import '../application/template_detail_controller.dart';

class TemplateDetailScreen extends ConsumerWidget {
  const TemplateDetailScreen({super.key, required this.templateId});

  final String templateId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(templateDetailControllerProvider(templateId));

    return Scaffold(
      appBar: AppBar(title: const Text('Template')),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(message: safeErrorMessage(error)),
        data: (template) => _TemplateDetailBody(template: template),
      ),
    );
  }
}

class _TemplateDetailBody extends ConsumerWidget {
  const _TemplateDetailBody({required this.template});

  final Template template;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isOwner = template.ownerId != null;
    final marketplace = ref.watch(marketplaceControllerProvider);
    final inMyList =
        marketplace.value?.any((t) => t.id == template.id) ?? false;

    TemplateDefinition? definition;
    String? definitionError;
    try {
      definition = TemplateDefinitionParser.parse(template.definitionJson);
    } on TemplateDefinitionParseException catch (e) {
      definitionError = e.message;
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(template.name, style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(template.category, style: Theme.of(context).textTheme.bodySmall),
        if (template.description.isNotEmpty) ...[
          const SizedBox(height: 8),
          Text(template.description),
        ],
        if (template.riskNoticeText.isNotEmpty) ...[
          const SizedBox(height: 12),
          WarningBanner(message: template.riskNoticeText),
        ],
        const SizedBox(height: 12),
        if (definition != null) ...[
          Text(
            '${definition.fields.length} fields · ${definition.clauses.length} optional clauses',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
        ] else if (definitionError != null) ...[
          WarningBanner(
            message: 'Template definition is invalid: $definitionError',
          ),
        ],
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Template body',
                  style: Theme.of(context).textTheme.titleSmall,
                ),
                const SizedBox(height: 8),
                Text(template.body.isEmpty ? '(no body text)' : template.body),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: () => context.push('/templates/${template.id}/fill'),
          icon: const Icon(Icons.edit_document),
          label: const Text('Fill in and generate'),
        ),
        const SizedBox(height: 8),
        PolishClauseButton(templateId: template.id),
        const SizedBox(height: 8),
        OutlinedButton.icon(
          onPressed: () => _export(context, ref),
          icon: const Icon(Icons.download_outlined),
          label: Text('Export revision ${template.currentRevision}'),
        ),
        if (template.sourceStableId != null) ...[
          const SizedBox(height: 4),
          Text(
            'Imported from ${template.sourceInstance ?? template.sourceStableId}',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
        if (!isOwner) ...[
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () async {
              final messenger = ScaffoldMessenger.of(context);
              final error = await ref
                  .read(marketplaceControllerProvider.notifier)
                  .copy(template.id);
              if (!context.mounted) return;
              if (error != null) {
                messenger.showSnackBar(SnackBar(content: Text(error)));
              } else {
                messenger.showSnackBar(
                  const SnackBar(content: Text('Copied to your templates.')),
                );
                context.pop();
              }
            },
            icon: const Icon(Icons.content_copy),
            label: const Text('Use as my template'),
          ),
        ],
        if (isOwner) ...[
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => _showRevisions(context, ref),
            icon: const Icon(Icons.history),
            label: const Text('Revision history'),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () =>
                      context.push('/templates/${template.id}/edit'),
                  icon: const Icon(Icons.edit_outlined),
                  label: const Text('Edit'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => _confirmDelete(context, ref),
                  icon: const Icon(Icons.delete_outline),
                  label: const Text('Delete'),
                ),
              ),
            ],
          ),
        ],
        if (!inMyList && !isOwner) ...[
          const SizedBox(height: 8),
          Text(
            'This template is shared. Copy it to make a private editable version.',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
      ],
    );
  }

  Future<void> _export(BuildContext context, WidgetRef ref) async {
    try {
      final bytes = await ref
          .read(apiClientProvider)
          .exportTemplate(template.id);
      await SharePlus.instance.share(
        ShareParams(
          files: [
            XFile.fromData(
              Uint8List.fromList(bytes),
              mimeType: 'application/json',
              name: '${template.name}.opendockify.json',
            ),
          ],
        ),
      );
    } catch (error) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(safeErrorMessage(error))),
        );
      }
    }
  }

  Future<void> _showRevisions(BuildContext context, WidgetRef ref) async {
    final revisions = await ref
        .read(apiClientProvider)
        .listTemplateRevisions(template.id);
    if (!context.mounted) return;
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Revision history'),
        content: SizedBox(
          width: 420,
          child: ListView(
            shrinkWrap: true,
            children: revisions
                .map(
                  (revision) => ListTile(
                    title: Text('Revision ${revision.revision}'),
                    subtitle: Text(
                      revision.createdAt?.toLocal().toString() ?? '',
                    ),
                    trailing: revision.revision == template.currentRevision
                        ? const Text('Current')
                        : TextButton(
                            onPressed: () async {
                              await ref
                                  .read(apiClientProvider)
                                  .rollbackTemplate(
                                    template.id,
                                    revision.revision,
                                  );
                              if (dialogContext.mounted) {
                                Navigator.pop(dialogContext);
                              }
                              ref.invalidate(
                                templateDetailControllerProvider(template.id),
                              );
                            },
                            child: const Text('Roll back'),
                          ),
                  ),
                )
                .toList(),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete template?'),
        content: const Text('This cannot be undone.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (confirmed != true) return;
    final error = await ref
        .read(marketplaceControllerProvider.notifier)
        .delete(template.id);
    if (!context.mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    if (error != null) {
      messenger.showSnackBar(SnackBar(content: Text(error)));
    } else {
      messenger.showSnackBar(
        const SnackBar(content: Text('Template deleted.')),
      );
      context.pop();
    }
  }
}
