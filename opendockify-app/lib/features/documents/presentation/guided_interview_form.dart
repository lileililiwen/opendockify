import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/document.dart';
import '../../../core/models/interview.dart';
import '../../../core/models/template.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';
import '../../templates/presentation/dynamic_template_form.dart';

class GuidedInterviewForm extends ConsumerStatefulWidget {
  const GuidedInterviewForm({
    super.key,
    required this.template,
    required this.onGenerated,
    this.draftKey,
  });

  final Template template;
  final String? draftKey;
  final ValueChanged<GenerateResult> onGenerated;

  @override
  ConsumerState<GuidedInterviewForm> createState() =>
      _GuidedInterviewFormState();
}

class _GuidedInterviewFormState extends ConsumerState<GuidedInterviewForm> {
  GlobalKey<DynamicTemplateFormState> _formKey =
      GlobalKey<DynamicTemplateFormState>();
  InterviewSession? _session;
  List<InterviewReviewItem> _review = const [];
  PreviewResult? _preview;
  bool _loading = true;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      InterviewResponse response;
      final key = widget.draftKey;
      final saved = key == null
          ? null
          : await ref.read(draftStoreProvider).read(key);
      if (saved?.interviewSessionId case final String id when id.isNotEmpty) {
        try {
          response = await ref.read(apiClientProvider).getInterview(id);
        } on ApiError {
          response = await ref
              .read(apiClientProvider)
              .createInterview(widget.template.id);
        }
      } else {
        response = await ref
            .read(apiClientProvider)
            .createInterview(widget.template.id);
      }
      await _accept(response);
    } on ApiError catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _accept(InterviewResponse response) async {
    final session = response.session;
    if (session == null) return;
    _formKey = GlobalKey<DynamicTemplateFormState>();
    if (mounted) {
      setState(() {
        _session = session;
        _review = response.review;
        _preview = response.preview;
        _error = null;
      });
    }
    final key = widget.draftKey;
    if (key != null) {
      await ref
          .read(draftStoreProvider)
          .write(
            key,
            LocalDocumentDraft(
              values: session.answers,
              selectedClauseIds: session.selectedClauseIds,
              updatedAt: DateTime.now().toUtc(),
              interviewSessionId: session.id,
            ),
          );
    }
  }

  Future<void> _next() async {
    final session = _session;
    final snapshot = _formKey.currentState?.submit();
    if (session == null || snapshot == null) return;
    await _run(() async {
      final response = await ref
          .read(apiClientProvider)
          .answerInterview(session.id, session.version, snapshot.values);
      await _accept(response);
      if (response.session?.readyForReview == true) await _loadReview();
    });
  }

  Future<void> _back() async {
    final session = _session;
    if (session == null) return;
    await _run(() async {
      await _accept(
        await ref
            .read(apiClientProvider)
            .backInterview(session.id, session.version),
      );
      if (mounted) {
        setState(() {
          _review = const [];
          _preview = null;
        });
      }
    });
  }

  Future<void> _loadReview() async {
    final session = _session;
    if (session == null) return;
    await _run(
      () async => _accept(
        await ref.read(apiClientProvider).reviewInterview(session.id),
      ),
    );
  }

  Future<void> _previewDocument() async {
    final session = _session;
    if (session == null) return;
    await _run(
      () async => _accept(
        await ref.read(apiClientProvider).completeInterview(session.id),
      ),
    );
  }

  Future<void> _finalize() async {
    final session = _session;
    if (session == null || _preview == null) return;
    await _run(() async {
      final result = await ref
          .read(apiClientProvider)
          .finalizeDocument(
            GenerateDocumentRequest(
              templateId: session.templateId,
              values: session.answers,
              selectedClauseIds: session.selectedClauseIds,
            ),
          );
      await ref.read(apiClientProvider).deleteInterview(session.id);
      if (widget.draftKey != null) {
        await ref.read(draftStoreProvider).clear(widget.draftKey!);
      }
      widget.onGenerated(result);
    });
  }

  Future<void> _run(Future<void> Function() action) async {
    if (_busy) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await action();
    } on ApiError catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_session == null) {
      return ErrorView(message: _error ?? 'Interview could not be loaded.');
    }
    final session = _session!;
    final step = session.currentStep;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          widget.template.name,
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        if (_error != null) ErrorView(message: _error!),
        if (step != null) ...[
          Semantics(
            label: 'Step ${step.position} of ${step.total}',
            child: LinearProgressIndicator(
              value: step.total == 0 ? 0 : step.position / step.total,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Step ${step.position} of ${step.total}',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          Text(step.title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 12),
          DynamicTemplateForm(
            key: _formKey,
            definition: TemplateDefinition(fields: step.fields),
            initialValues: session.answers,
          ),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: _busy || step.position == 1 ? null : _back,
                  child: const Text('Back'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: FilledButton(
                  onPressed: _busy ? null : _next,
                  child: const Text('Save and continue'),
                ),
              ),
            ],
          ),
        ] else ...[
          Text(
            'Review answers',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          for (final section in _review)
            Card(
              child: ListTile(
                title: Text(section.label),
                subtitle: Text(
                  section.answers.entries
                      .map((entry) => '${entry.key}: ${entry.value}')
                      .join('\n'),
                ),
              ),
            ),
          if (_preview case final preview?) ...[
            for (final warning in preview.warnings)
              WarningBanner(message: warning),
            const SizedBox(height: 8),
            SelectableText(preview.renderedText),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: _busy ? null : _back,
                  child: const Text('Back to answers'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: FilledButton(
                  onPressed: _busy
                      ? null
                      : (_preview == null ? _previewDocument : _finalize),
                  child: Text(
                    _preview == null ? 'Preview document' : 'Finalize',
                  ),
                ),
              ),
            ],
          ),
        ],
      ],
    );
  }
}
