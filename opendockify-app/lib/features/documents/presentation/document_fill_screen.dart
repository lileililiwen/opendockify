import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/document.dart';
import '../../../core/models/template.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';
import '../../templates/presentation/dynamic_template_form.dart';

/// Fills a template form and generates a document, or re-edits an existing
/// document (creating a new immutable version).
class DocumentFillScreen extends ConsumerStatefulWidget {
  const DocumentFillScreen({super.key, this.templateId, this.documentId});

  final String? templateId;
  final String? documentId;

  @override
  ConsumerState<DocumentFillScreen> createState() => _DocumentFillScreenState();
}

class _DocumentFillScreenState extends ConsumerState<DocumentFillScreen> {
  final _templateFormKey = GlobalKey<DynamicTemplateFormState>();
  bool _loading = true;
  bool _busy = false;
  String? _error;

  Template? _template;
  TemplateDefinition? _definition;
  Map<String, String> _initialValues = const {};
  Set<String> _initialClauses = const {};
  String? _effectiveTemplateId;
  bool _isReedit = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final api = ref.read(apiClientProvider);
    String templateId = widget.templateId ?? '';
    try {
      if (widget.documentId != null) {
        _isReedit = true;
        final doc = await api.getDocument(widget.documentId!);
        templateId = doc.templateId;
        _parseSnapshot(doc.snapshotJson);
      }
      _effectiveTemplateId = templateId;
      final template = await api.getTemplate(templateId);
      _template = template;
      _definition = TemplateDefinitionParser.parse(template.definitionJson);
      if (mounted) setState(() => _loading = false);
    } on ApiError catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.message;
        });
      }
    } on TemplateDefinitionParseException catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = 'Template definition is invalid: ${e.message}';
        });
      }
    }
  }

  void _parseSnapshot(String snapshotJson) {
    if (snapshotJson.trim().isEmpty) return;
    try {
      final decoded = jsonDecode(snapshotJson);
      if (decoded is Map<String, dynamic>) {
        final values = decoded['values'];
        final clauses = decoded['selectedClauseIds'];
        if (values is Map) {
          _initialValues = values.map((k, v) => MapEntry(k.toString(), v?.toString() ?? ''));
        }
        if (clauses is List) {
          _initialClauses = clauses.whereType<String>().toSet();
        }
      }
    } on FormatException {
      // Ignore malformed snapshots; start empty.
    }
  }

  Future<void> _submit() async {
    final formState = _templateFormKey.currentState;
    if (formState == null) return;
    final result = formState.submit();
    if (result == null) return;

    final request = GenerateDocumentRequest(
      templateId: _effectiveTemplateId!,
      values: result.values,
      selectedClauseIds: result.selectedClauseIds,
    );

    setState(() {
      _busy = true;
      _error = null;
    });
    formState.setSubmitting(true);

    final api = ref.read(apiClientProvider);
    try {
      final GenerateResult generated;
      if (_isReedit) {
        generated = await api.reeditDocument(widget.documentId!, request);
      } else {
        generated = await api.generateDocument(request);
      }
      if (!mounted) return;
      setState(() => _busy = false);
      formState.setSubmitting(false);
      _showResult(generated);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.message;
      });
      formState.setSubmitting(false);
    }
  }

  void _showResult(GenerateResult result) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (context) => _GenerationResultSheet(
        result: result,
        onOpen: () {
          Navigator.of(context).pop();
          context.push('/documents/${result.document.id}');
        },
        onDismiss: () => Navigator.of(context).pop(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_isReedit ? 'Re-edit document' : 'Fill in template')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? ErrorView(message: _error!)
              : _buildForm(context),
    );
  }

  Widget _buildForm(BuildContext context) {
    final template = _template!;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(template.name, style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 8),
        DynamicTemplateForm(
          key: _templateFormKey,
          definition: _definition!,
          riskNoticeText: template.riskNoticeText,
          initialValues: _initialValues,
          initialClauseIds: _initialClauses,
        ),
        const SizedBox(height: 24),
        FilledButton.icon(
          onPressed: _busy ? null : _submit,
          icon: _busy
              ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.auto_awesome),
          label: Text(_isReedit ? 'Save new version' : 'Generate document'),
        ),
        const SizedBox(height: 24),
      ],
    );
  }
}

class _GenerationResultSheet extends StatelessWidget {
  const _GenerationResultSheet({required this.result, required this.onOpen, required this.onDismiss});

  final GenerateResult result;
  final VoidCallback onOpen;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    final doc = result.document;
    final warnings = result.warnings;
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Document generated', style: Theme.of(context).textTheme.titleLarge),
            if (warnings.isNotEmpty) ...[
              const SizedBox(height: 8),
              for (final w in warnings) WarningBanner(message: w),
            ],
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 300),
              child: SingleChildScrollView(
                child: SelectableText(doc.renderedText.isEmpty ? '(no text)' : doc.renderedText),
              ),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: onOpen,
              icon: const Icon(Icons.visibility),
              label: const Text('View document'),
            ),
            const SizedBox(height: 8),
            OutlinedButton(onPressed: onDismiss, child: const Text('Close')),
          ],
        ),
      ),
    );
  }
}