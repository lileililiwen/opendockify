import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/template.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';

/// Administrator upsert of a public (global) template.
class AdminTemplateUpsertScreen extends ConsumerStatefulWidget {
  const AdminTemplateUpsertScreen({super.key});

  @override
  ConsumerState<AdminTemplateUpsertScreen> createState() => _AdminTemplateUpsertScreenState();
}

class _AdminTemplateUpsertScreenState extends ConsumerState<AdminTemplateUpsertScreen> {
  final _formKey = GlobalKey<FormState>();
  final _name = TextEditingController();
  final _category = TextEditingController();
  final _description = TextEditingController();
  final _riskNotice = TextEditingController();
  final _body = TextEditingController();
  final _definition = TextEditingController();
  final _id = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _category.dispose();
    _description.dispose();
    _riskNotice.dispose();
    _body.dispose();
    _definition.dispose();
    _id.dispose();
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
      final id = _id.text.trim().isEmpty ? null : _id.text.trim();
      await ref.read(apiClientProvider).adminUpsertTemplate(id: id, request: request);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Global template saved.')));
      context.pop();
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
      appBar: AppBar(title: const Text('Global template')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            if (_error != null) ...[
              WarningBanner(message: _error!),
              const SizedBox(height: 8),
            ],
            TextFormField(
              controller: _id,
              enabled: !_busy,
              decoration: const InputDecoration(
                labelText: 'Template id (blank to create new)',
                hintText: 'Leave blank to create a new template',
              ),
            ),
            const SizedBox(height: 16),
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
            TextFormField(controller: _description, enabled: !_busy, decoration: const InputDecoration(labelText: 'Description'), maxLines: 2),
            const SizedBox(height: 16),
            TextFormField(controller: _riskNotice, enabled: !_busy, decoration: const InputDecoration(labelText: 'Risk notice text'), maxLines: 2),
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
              decoration: const InputDecoration(labelText: 'Definition JSON', alignLabelWithHint: true),
              maxLines: 10,
            ),
            const SizedBox(height: 24),
            FilledButton(
              onPressed: _busy ? null : _save,
              child: _busy
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Save global template'),
            ),
          ],
        ),
      ),
    );
  }
}