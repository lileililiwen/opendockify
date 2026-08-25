import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';

class TemplatePackageImportScreen extends ConsumerStatefulWidget {
  const TemplatePackageImportScreen({super.key});
  @override
  ConsumerState<TemplatePackageImportScreen> createState() =>
      _TemplatePackageImportScreenState();
}

class _TemplatePackageImportScreenState
    extends ConsumerState<TemplatePackageImportScreen> {
  List<int>? _bytes;
  PackageValidation? _validation;
  String _policy = 'create-copy';
  bool _busy = false;

  Future<void> _pickAndValidate() async {
    final files = await FilePicker.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['json'],
    );
    if (files.isEmpty) return;
    final bytes = await files.single.readAsBytes();
    setState(() => _busy = true);
    try {
      final validation = await ref
          .read(apiClientProvider)
          .validateTemplatePackage(bytes);
      setState(() {
        _bytes = bytes;
        _validation = validation;
        _policy = validation.conflict ? 'reject' : 'create-copy';
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _import() async {
    final validation = _validation;
    if (_bytes == null || validation == null) return;
    setState(() => _busy = true);
    try {
      final template = await ref
          .read(apiClientProvider)
          .importTemplatePackage(_bytes!, validation.receipt, _policy);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(
              'Imported ${template.name} revision ${template.currentRevision}.',
            ),
          ),
        );
      }
      setState(() {
        _bytes = null;
        _validation = null;
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Import template package')),
    body: ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'Packages are validated for version, size, definition, and SHA-256 integrity before import.',
        ),
        const SizedBox(height: 16),
        FilledButton.icon(
          onPressed: _busy ? null : _pickAndValidate,
          icon: const Icon(Icons.upload_file),
          label: const Text('Choose and validate package'),
        ),
        if (_validation case final validation?) ...[
          const SizedBox(height: 16),
          ListTile(
            title: const Text('Digest verified'),
            subtitle: Text(validation.digest),
            leading: const Icon(Icons.verified_outlined),
          ),
          if (validation.conflict)
            const ListTile(
              title: Text('Stable identity conflict'),
              subtitle: Text('Select an explicit conflict policy.'),
            ),
          DropdownButtonFormField<String>(
            initialValue: _policy,
            decoration: const InputDecoration(labelText: 'Import policy'),
            items:
                (validation.conflict
                        ? ['reject', 'create-copy', 'new-revision']
                        : ['create-copy'])
                    .map(
                      (value) =>
                          DropdownMenuItem(value: value, child: Text(value)),
                    )
                    .toList(),
            onChanged: (value) => setState(() => _policy = value ?? _policy),
          ),
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _busy ? null : _import,
            child: const Text('Confirm import'),
          ),
        ],
      ],
    ),
  );
}
