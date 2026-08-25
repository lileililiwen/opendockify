import 'dart:async';
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
import 'guided_interview_form.dart';
import '../../auth/application/session_controller.dart';

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
  Timer? _draftDebounce;
  LocalDocumentDraft? _pendingDraft;
  String? _draftKey;
  bool _hasSavedDraft = false;
  bool _draftSaved = false;

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

  @override
  void dispose() {
    _draftDebounce?.cancel();
    if (_pendingDraft != null) unawaited(_flushDraft());
    super.dispose();
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
      await _restoreDraft(templateId);
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

  Future<void> _restoreDraft(String templateId) async {
    final userId = ref.read(sessionControllerProvider).profile?.id;
    if (userId == null || userId.isEmpty) return;
    final formId = _isReedit
        ? 'reedit_${widget.documentId}'
        : 'template_$templateId';
    _draftKey = DraftStorageKey.forForm(userId: userId, formId: formId);
    try {
      final draft = await ref.read(draftStoreProvider).read(_draftKey!);
      if (draft == null) return;
      _initialValues = draft.values;
      _initialClauses = draft.selectedClauseIds.toSet();
      _hasSavedDraft = true;
      _draftSaved = true;
    } catch (_) {
      // Secure-storage failures must not block drafting.
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
          _initialValues = values.map(
            (k, v) => MapEntry(k.toString(), v?.toString() ?? ''),
          );
        }
        if (clauses is List) {
          _initialClauses = clauses.whereType<String>().toSet();
        }
      }
    } on FormatException {
      // Ignore malformed snapshots; start empty.
    }
  }

  void _onDraftChanged(
    ({Map<String, String> values, List<String> selectedClauseIds}) snapshot,
  ) {
    if (_draftKey == null) return;
    _pendingDraft = LocalDocumentDraft(
      values: snapshot.values,
      selectedClauseIds: snapshot.selectedClauseIds,
      updatedAt: DateTime.now().toUtc(),
    );
    _draftDebounce?.cancel();
    if (mounted) setState(() => _draftSaved = false);
    _draftDebounce = Timer(
      const Duration(milliseconds: 400),
      () => unawaited(_flushDraft()),
    );
  }

  Future<void> _flushDraft() async {
    final key = _draftKey;
    final draft = _pendingDraft;
    if (key == null || draft == null) return;
    try {
      await ref.read(draftStoreProvider).write(key, draft);
      _hasSavedDraft = true;
      if (mounted) setState(() => _draftSaved = true);
    } catch (_) {
      if (mounted) setState(() => _draftSaved = false);
    }
  }

  Future<void> _discardSavedDraft() async {
    final key = _draftKey;
    if (key == null) return;
    _draftDebounce?.cancel();
    _pendingDraft = null;
    await ref.read(draftStoreProvider).clear(key);
    if (!mounted) return;
    setState(() {
      _hasSavedDraft = false;
      _draftSaved = false;
    });
    ScaffoldMessenger.of(context)
        .showSnackBar(const SnackBar(content: Text('Saved draft discarded.')));
  }

  GenerateDocumentRequest? _collectRequest() {
    final result = _templateFormKey.currentState?.submit();
    if (result == null || _effectiveTemplateId == null) return null;
    return GenerateDocumentRequest(
      templateId: _effectiveTemplateId!,
      values: result.values,
      selectedClauseIds: result.selectedClauseIds,
    );
  }

  Future<void> _preview() async {
    final request = _collectRequest();
    if (request == null) return;
    final formState = _templateFormKey.currentState!;
    setState(() {
      _busy = true;
      _error = null;
    });
    formState.setSubmitting(true);
    try {
      final preview = await ref
          .read(apiClientProvider)
          .previewDocument(request);
      if (!mounted) return;
      setState(() => _busy = false);
      formState.setSubmitting(false);
      await showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        builder: (context) => _PreviewResultSheet(preview: preview),
      );
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.message;
      });
      formState.setSubmitting(false);
    }
  }

  Future<void> _finalize() async {
    final formState = _templateFormKey.currentState;
    if (formState == null) return;
    final request = _collectRequest();
    if (request == null) return;

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
        generated = await api.finalizeDocument(request);
      }
      if (_draftKey != null) {
        await ref.read(draftStoreProvider).clear(_draftKey!);
      }
      if (!mounted) return;
      setState(() {
        _busy = false;
        _hasSavedDraft = false;
        _draftSaved = false;
        _pendingDraft = null;
      });
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
      appBar: AppBar(
        title: Text(_isReedit ? 'Re-edit document' : 'Fill in template'),
        actions: [
          if (_hasSavedDraft)
            IconButton(
              onPressed: _busy ? null : _discardSavedDraft,
              icon: const Icon(Icons.delete_sweep_outlined),
              tooltip: 'Discard saved draft',
            ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
          ? ErrorView(message: _error!)
          : _buildForm(context),
    );
  }

  Widget _buildForm(BuildContext context) {
    final template = _template!;
    if (!_isReedit && _definition!.interview != null) {
      return GuidedInterviewForm(
        template: template,
        draftKey: _draftKey,
        onGenerated: _showResult,
      );
    }
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(template.name, style: Theme.of(context).textTheme.titleLarge),
        if (_draftKey != null) ...[
          const SizedBox(height: 4),
          Text(
            _draftSaved
                ? 'Draft saved securely on this device'
                : 'Saving draft…',
            style: Theme.of(context).textTheme.bodySmall,
          ),
        ],
        const SizedBox(height: 8),
        DynamicTemplateForm(
          key: _templateFormKey,
          definition: _definition!,
          riskNoticeText: template.riskNoticeText,
          initialValues: _initialValues,
          initialClauseIds: _initialClauses,
          onDraftChanged: _onDraftChanged,
        ),
        const SizedBox(height: 24),
        Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                onPressed: _busy ? null : _preview,
                icon: const Icon(Icons.preview_outlined),
                label: const Text('Preview'),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: FilledButton.icon(
                onPressed: _busy ? null : _finalize,
                icon: _busy
                    ? const SizedBox(
                        height: 18,
                        width: 18,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.check_circle_outline),
                label: Text(_isReedit ? 'Finalize version' : 'Finalize'),
              ),
            ),
          ],
        ),
        const SizedBox(height: 24),
      ],
    );
  }
}

class _PreviewResultSheet extends StatelessWidget {
  const _PreviewResultSheet({required this.preview});

  final PreviewResult preview;

  @override
  Widget build(BuildContext context) {
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Document preview',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            Text(
              'Not finalized · no PDF or document has been created',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            if (preview.warnings.isNotEmpty) ...[
              const SizedBox(height: 8),
              for (final warning in preview.warnings)
                WarningBanner(message: warning),
            ],
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: BoxConstraints(
                maxHeight: MediaQuery.sizeOf(context).height * 0.6,
              ),
              child: SingleChildScrollView(
                child: SelectableText(
                  preview.renderedText.isEmpty
                      ? '(no text)'
                      : preview.renderedText,
                ),
              ),
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(),
              child: const Text('Continue editing'),
            ),
          ],
        ),
      ),
    );
  }
}

class _GenerationResultSheet extends StatelessWidget {
  const _GenerationResultSheet({
    required this.result,
    required this.onOpen,
    required this.onDismiss,
  });

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
            Text(
              'Document generated',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            if (warnings.isNotEmpty) ...[
              const SizedBox(height: 8),
              for (final w in warnings) WarningBanner(message: w),
            ],
            const SizedBox(height: 12),
            ConstrainedBox(
              constraints: const BoxConstraints(maxHeight: 300),
              child: SingleChildScrollView(
                child: SelectableText(
                  doc.renderedText.isEmpty ? '(no text)' : doc.renderedText,
                ),
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
