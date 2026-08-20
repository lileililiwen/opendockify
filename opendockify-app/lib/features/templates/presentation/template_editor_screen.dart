import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/template.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';

/// Create or edit a private template (name, category, description, risk
/// notice, body, definition JSON).
class TemplateEditorScreen extends ConsumerStatefulWidget {
  const TemplateEditorScreen({super.key, this.templateId});

  final String? templateId;

  @override
  ConsumerState<TemplateEditorScreen> createState() => _TemplateEditorScreenState();
}

class _TemplateEditorScreenState extends ConsumerState<TemplateEditorScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _category = TextEditingController();
  final _description = TextEditingController();
  final _riskNotice = TextEditingController();
  final _body = TextEditingController();
  final _definition = TextEditingController();
  bool _loading = true;
  bool _busy = false;
  String? _error;

  bool get _isEditing => widget.templateId != null;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    if (!_isEditing) {
      setState(() => _loading = false);
      return;
    }
    try {
      final template = await ref.read(apiClientProvider).getTemplate(widget.templateId!);
      _name.text = template.name;
      _category.text = template.category;
      _description.text = template.description;
      _riskNotice.text = template.riskNoticeText;
      _body.text = template.body;
      _definition.text = template.definitionJson;
      if (mounted) setState(() => _loading = false);
    } on ApiError catch (e) {
      if (mounted) {
        setState(() {
          _loading = false;
          _error = e.message;
        });
      }
    }
  }

  @override
  void dispose() {
    _name.dispose();
    _category.dispose();
    _description.dispose();
    _riskNotice.dispose();
    _body.dispose();
    _definition.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;

    final definitionJson = _definition.text.trim();
    try {
      TemplateDefinitionParser.parse(definitionJson);
    } on TemplateDefinitionParseException catch (e) {
      setState(() => _error = 'Definition JSON is invalid: ${e.message}');
      return;
    }

    final request = CreateTemplateRequest(
      name: _name.text.trim(),
      category: _category.text.trim(),
      description: _description.text.trim().isEmpty ? null : _description.text.trim(),
      riskNoticeText: _riskNotice.text.trim().isEmpty ? null : _riskNotice.text.trim(),
      body: _body.text,
      definitionJson: definitionJson,
    );

    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      if (_isEditing) {
        await ref.read(apiClientProvider).updateTemplate(widget.templateId!, request);
      } else {
        await ref.read(apiClientProvider).createTemplate(request);
      }
      if (!mounted) return;
      context.pop(true);
    } on ApiError catch (e) {
      if (mounted) {
        setState(() {
          _busy = false;
          _error = e.message;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_isEditing ? 'Edit template' : 'New template')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : Form(
              key: _formKey,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  if (_error != null) ...[
                    WarningBanner(message: _error!),
                    const SizedBox(height: 8),
                  ],
                  TextFormField(
                    controller: _name,
                    enabled: !_busy,
                    decoration: const InputDecoration(labelText: 'Name', prefixText: '* '),
                    validator: (v) => (v == null || v.trim().isEmpty) ? 'Name is required.' : null,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _category,
                    enabled: !_busy,
                    decoration: const InputDecoration(labelText: 'Category', prefixText: '* '),
                    validator: (v) => (v == null || v.trim().isEmpty) ? 'Category is required.' : null,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _description,
                    enabled: !_busy,
                    decoration: const InputDecoration(labelText: 'Description'),
                    maxLines: 2,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _riskNotice,
                    enabled: !_busy,
                    decoration: const InputDecoration(labelText: 'Risk notice text'),
                    maxLines: 2,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _body,
                    enabled: !_busy,
                    decoration: const InputDecoration(labelText: 'Body (with {{field}} placeholders)', alignLabelWithHint: true),
                    maxLines: 8,
                  ),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: _definition,
                    enabled: !_busy,
                    decoration: const InputDecoration(
                      labelText: 'Definition JSON',
                      alignLabelWithHint: true,
                      hintText: '{"fields":[{"name":"amount","label":"Amount","type":"Currency","required":true}],"clauses":[]}',
                    ),
                    maxLines: 10,
                  ),
                  const SizedBox(height: 24),
                  FilledButton(
                    onPressed: _busy ? null : _save,
                    child: _busy
                        ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                        : Text(_isEditing ? 'Save changes' : 'Create template'),
                  ),
                ],
              ),
            ),
    );
  }
}