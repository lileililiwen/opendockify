import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:share_plus/share_plus.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/document.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';
import '../../../core/errors/error_presenter.dart';
import '../../ai/presentation/polish_document_button.dart';
import '../application/documents_controller.dart';
import '../data/pdf_export_service.dart';

class DocumentDetailScreen extends ConsumerWidget {
  const DocumentDetailScreen({super.key, required this.documentId});

  final String documentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(documentDetailControllerProvider(documentId));

    return Scaffold(
      appBar: AppBar(title: const Text('Document')),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(message: safeErrorMessage(error)),
        data: (doc) => _DocumentDetailBody(document: doc),
      ),
    );
  }
}

class _DocumentDetailBody extends ConsumerWidget {
  const _DocumentDetailBody({required this.document});

  final DocumentView document;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          document.title.isEmpty ? 'Untitled document' : document.title,
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 4),
        Text(
          document.templateName,
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        if (document.isArchived) ...[
          const SizedBox(height: 8),
          const Align(
            alignment: Alignment.centerLeft,
            child: Chip(label: Text('Archived')),
          ),
        ],
        const SizedBox(height: 8),
        Text(
          'Generated ${_formatDate(document.createdAt)}',
          style: Theme.of(context).textTheme.bodySmall,
        ),
        const SizedBox(height: 8),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: SelectableText(
              document.renderedText.isEmpty
                  ? '(no text)'
                  : document.renderedText,
            ),
          ),
        ),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: () => _downloadPdf(context, ref),
          icon: const Icon(Icons.download),
          label: const Text('Download PDF'),
        ),
        if (!document.isOwner) ...[
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerLeft,
            child: Chip(label: Text('Shared ${document.accessLevel} access')),
          ),
        ],
        if (document.isOwner) ...[
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => _showSharing(context, ref),
            icon: const Icon(Icons.share_outlined),
            label: const Text('Sharing and access history'),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => context.push('/documents/${document.id}/reedit'),
            icon: const Icon(Icons.edit_document),
            label: const Text('Re-edit (new version)'),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => _rename(context, ref),
                  icon: const Icon(Icons.drive_file_rename_outline),
                  label: const Text('Rename'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: () => _toggleArchive(context, ref),
                  icon: Icon(
                    document.isArchived
                        ? Icons.unarchive_outlined
                        : Icons.archive_outlined,
                  ),
                  label: Text(document.isArchived ? 'Restore' : 'Archive'),
                ),
              ),
            ],
          ),
        ],
        const SizedBox(height: 8),
        _buildVersionHistory(context, ref),
        const SizedBox(height: 8),
        if (document.isOwner) ...[
          PolishDocumentButton(document: document),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: () => _confirmDelete(context, ref),
            icon: const Icon(Icons.delete_outline),
            label: const Text('Delete'),
          ),
        ],
      ],
    );
  }

  Future<void> _showSharing(BuildContext context, WidgetRef ref) async {
    var shares = await ref
        .read(apiClientProvider)
        .listDocumentShares(document.id);
    if (!context.mounted) return;
    await showDialog<void>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (context, setDialogState) {
          Future<void> refresh() async {
            shares = await ref
                .read(apiClientProvider)
                .listDocumentShares(document.id);
            setDialogState(() {});
          }

          return AlertDialog(
            title: const Text('Sharing'),
            content: SizedBox(
              width: 520,
              child: ListView(
                shrinkWrap: true,
                children: [
                  const Text('Internal grants'),
                  for (final share in shares.where((x) => x.kind == 'grant'))
                    ListTile(
                      title: Text(share.username ?? 'User'),
                      subtitle: Text(
                        '${share.accessLevel}${share.revokedAt == null ? '' : ' · revoked'}',
                      ),
                      trailing: share.revokedAt == null
                          ? IconButton(
                              icon: const Icon(Icons.block),
                              onPressed: () async {
                                await ref
                                    .read(apiClientProvider)
                                    .revokeDocumentShare(document.id, share);
                                await refresh();
                              },
                            )
                          : null,
                    ),
                  const Divider(),
                  const Text('External links'),
                  for (final share in shares.where((x) => x.kind == 'link'))
                    ListTile(
                      title: Text(
                        share.allowDownload == true
                            ? 'View and download'
                            : 'View only',
                      ),
                      subtitle: Text(
                        '${share.expiresAt?.toLocal() ?? ''}${share.revokedAt == null ? '' : ' · revoked'}',
                      ),
                      trailing: share.revokedAt == null
                          ? IconButton(
                              icon: const Icon(Icons.link_off),
                              onPressed: () async {
                                await ref
                                    .read(apiClientProvider)
                                    .revokeDocumentShare(document.id, share);
                                await refresh();
                              },
                            )
                          : null,
                    ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => _showAudit(dialogContext, ref),
                child: const Text('Access history'),
              ),
              TextButton(
                onPressed: () async {
                  await _createGrant(dialogContext, ref);
                  await refresh();
                },
                child: const Text('Grant user'),
              ),
              TextButton(
                onPressed: () async {
                  await _createLink(dialogContext, ref);
                  await refresh();
                },
                child: const Text('Create link'),
              ),
              TextButton(
                onPressed: () => Navigator.pop(dialogContext),
                child: const Text('Close'),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<void> _createGrant(BuildContext context, WidgetRef ref) async {
    final username = TextEditingController();
    var level = 'view';
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: const Text('Grant document access'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: username,
                decoration: const InputDecoration(labelText: 'Username'),
              ),
              DropdownButtonFormField<String>(
                initialValue: level,
                items: const [
                  DropdownMenuItem(value: 'view', child: Text('View')),
                  DropdownMenuItem(value: 'review', child: Text('Review')),
                ],
                onChanged: (value) => setState(() => level = value ?? level),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Grant'),
            ),
          ],
        ),
      ),
    );
    if (confirmed == true) {
      await ref
          .read(apiClientProvider)
          .createDocumentGrant(document.id, username.text.trim(), level);
    }
    username.dispose();
  }

  Future<void> _createLink(BuildContext context, WidgetRef ref) async {
    var hours = 24;
    var download = false;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setState) => AlertDialog(
          title: const Text('Create external link'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              DropdownButtonFormField<int>(
                initialValue: hours,
                items: const [
                  DropdownMenuItem(value: 1, child: Text('1 hour')),
                  DropdownMenuItem(value: 24, child: Text('24 hours')),
                  DropdownMenuItem(value: 72, child: Text('72 hours')),
                ],
                onChanged: (value) => setState(() => hours = value ?? hours),
              ),
              SwitchListTile(
                value: download,
                onChanged: (value) => setState(() => download = value),
                title: const Text('Allow PDF download'),
              ),
            ],
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Create'),
            ),
          ],
        ),
      ),
    );
    if (confirmed != true || !context.mounted) return;
    final link = await ref
        .read(apiClientProvider)
        .createDocumentShareLink(document.id, hours, download);
    final url = ref.read(apiClientProvider).absoluteUrl('/s/${link.token}');
    if (!context.mounted) return;
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Copy this link now'),
        content: SelectableText(
          '$url\n\nThe secret cannot be retrieved after this dialog closes.',
        ),
        actions: [
          OutlinedButton.icon(
            onPressed: () async {
              await SharePlus.instance.share(ShareParams(text: url));
              if (context.mounted) Navigator.pop(context);
            },
            icon: const Icon(Icons.share_outlined),
            label: const Text('Share once'),
          ),
          FilledButton.icon(
            onPressed: () async {
              await Clipboard.setData(ClipboardData(text: url));
              if (context.mounted) Navigator.pop(context);
            },
            icon: const Icon(Icons.copy),
            label: const Text('Copy once'),
          ),
        ],
      ),
    );
  }

  Future<void> _showAudit(BuildContext context, WidgetRef ref) async {
    final events = await ref
        .read(apiClientProvider)
        .getDocumentShareAudit(document.id);
    if (!context.mounted) return;
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Access history'),
        content: SizedBox(
          width: 480,
          child: ListView(
            shrinkWrap: true,
            children: [
              for (final event in events)
                ListTile(
                  leading: Icon(
                    event.succeeded ? Icons.check_circle_outline : Icons.block,
                  ),
                  title: Text(event.action),
                  subtitle: Text(
                    '${event.actorCategory} · ${event.createdAt?.toLocal() ?? ''}',
                  ),
                ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  Widget _buildVersionHistory(BuildContext context, WidgetRef ref) {
    final versions = ref.watch(documentVersionsProvider(document.id));
    return Card(
      child: ExpansionTile(
        leading: const Icon(Icons.history),
        title: const Text('Version history'),
        subtitle: versions.value == null
            ? null
            : Text(
                '${versions.value!.length} version${versions.value!.length == 1 ? '' : 's'}',
              ),
        children: [
          versions.when(
            loading: () => const Padding(
              padding: EdgeInsets.all(16),
              child: CircularProgressIndicator(),
            ),
            error: (error, _) => Padding(
              padding: const EdgeInsets.all(16),
              child: Text(safeErrorMessage(error)),
            ),
            data: (items) => Column(
              children: [
                for (var index = 0; index < items.length; index++)
                  ListTile(
                    leading: CircleAvatar(child: Text('${index + 1}')),
                    title: Text(items[index].title),
                    subtitle: Text(_formatDate(items[index].createdAt)),
                    trailing: items[index].id == document.id
                        ? const Icon(Icons.check_circle_outline)
                        : null,
                    onTap: items[index].id == document.id
                        ? null
                        : () => context.push('/documents/${items[index].id}'),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _rename(BuildContext context, WidgetRef ref) async {
    final controller = TextEditingController(text: document.title);
    final title = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Rename document'),
        content: TextField(
          controller: controller,
          autofocus: true,
          maxLength: 200,
          decoration: const InputDecoration(labelText: 'Document title'),
          onSubmitted: (value) => Navigator.of(dialogContext).pop(value),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(controller.text),
            child: const Text('Save'),
          ),
        ],
      ),
    );
    controller.dispose();
    if (title == null || !context.mounted) return;
    final error = await ref
        .read(documentDetailControllerProvider(document.id).notifier)
        .updateMetadata(title: title, isArchived: document.isArchived);
    if (!context.mounted || error == null) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(error)));
  }

  Future<void> _toggleArchive(BuildContext context, WidgetRef ref) async {
    final error = await ref
        .read(documentDetailControllerProvider(document.id).notifier)
        .updateMetadata(
          title: document.title,
          isArchived: !document.isArchived,
        );
    if (!context.mounted) return;
    final message =
        error ??
        (document.isArchived ? 'Document restored.' : 'Document archived.');
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
  }

  Future<void> _downloadPdf(BuildContext context, WidgetRef ref) async {
    final messenger = ScaffoldMessenger.of(context);
    final downloadUrl = document.downloadUrl;
    if (downloadUrl == null || downloadUrl.isEmpty) {
      messenger.showSnackBar(
        const SnackBar(content: Text('No PDF available.')),
      );
      return;
    }
    try {
      final path = await ref
          .read(pdfExportServiceProvider)
          .download(downloadUrl, document.id);
      if (!context.mounted) return;
      await showModalBottomSheet<void>(
        context: context,
        builder: (context) => SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'PDF downloaded',
                  textAlign: TextAlign.center,
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 16),
                FilledButton.icon(
                  onPressed: () async {
                    Navigator.of(context).pop();
                    try {
                      await ref.read(pdfExportServiceProvider).open(path);
                    } catch (e) {
                      if (context.mounted) {
                        messenger.showSnackBar(
                          SnackBar(content: Text(e.toString())),
                        );
                      }
                    }
                  },
                  icon: const Icon(Icons.visibility),
                  label: const Text('Open'),
                ),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: () async {
                    Navigator.of(context).pop();
                    await ref.read(pdfExportServiceProvider).share(path);
                  },
                  icon: const Icon(Icons.share),
                  label: const Text('Share'),
                ),
              ],
            ),
          ),
        ),
      );
    } on ApiError catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    } catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.toString())));
    }
  }

  Future<void> _confirmDelete(BuildContext context, WidgetRef ref) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete document?'),
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
    try {
      await ref.read(apiClientProvider).deleteDocument(document.id);
      ref.invalidate(documentsControllerProvider);
      if (!context.mounted) return;
      final messenger = ScaffoldMessenger.of(context);
      messenger.showSnackBar(
        const SnackBar(content: Text('Document deleted.')),
      );
      context.go('/documents');
    } on ApiError catch (e) {
      if (!context.mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  String _formatDate(DateTime? date) {
    if (date == null) return '—';
    return '${date.year}-${date.month.toString().padLeft(2, '0')}-${date.day.toString().padLeft(2, '0')} '
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }
}
