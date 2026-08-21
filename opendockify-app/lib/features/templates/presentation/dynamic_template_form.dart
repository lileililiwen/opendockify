import 'package:flutter/material.dart';

import '../../../core/models/template.dart';
import '../../../core/widgets/common.dart';

/// Renders a fill-in form generated from a template definition: one input per
/// field (text/number/currency/date) and one toggle per optional clause, with
/// client-side validation mirroring the backend rules.
class DynamicTemplateForm extends StatefulWidget {
  const DynamicTemplateForm({
    super.key,
    required this.definition,
    this.riskNoticeText,
    this.initialValues = const {},
    this.initialClauseIds = const {},
    this.onDraftChanged,
  });

  final TemplateDefinition definition;
  final String? riskNoticeText;
  final Map<String, String> initialValues;
  final Set<String> initialClauseIds;
  final void Function(
    ({Map<String, String> values, List<String> selectedClauseIds}) snapshot,
  )?
  onDraftChanged;

  @override
  State<DynamicTemplateForm> createState() => DynamicTemplateFormState();
}

class DynamicTemplateFormState extends State<DynamicTemplateForm> {
  final _formKey = GlobalKey<FormState>();
  late final Map<String, TextEditingController> _controllers;
  late final Set<String> _selectedClauseIds;

  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _controllers = {
      for (final field in widget.definition.fields)
        field.name: TextEditingController(
          text: widget.initialValues[field.name] ?? '',
        ),
    };
    _selectedClauseIds = {...widget.initialClauseIds};
  }

  @override
  void dispose() {
    for (final c in _controllers.values) {
      c.dispose();
    }
    super.dispose();
  }

  /// Validates and collects the filled values. Returns null when validation
  /// fails; otherwise returns the values map and selected clause ids.
  ({Map<String, String> values, List<String> selectedClauseIds})? submit() {
    if (!_formKey.currentState!.validate()) return null;
    return snapshot();
  }

  /// Collects current input without validation so incomplete work can autosave.
  ({Map<String, String> values, List<String> selectedClauseIds}) snapshot() {
    final values = <String, String>{
      for (final field in widget.definition.fields)
        field.name: _controllers[field.name]!.text.trim(),
    };
    return (
      values: values,
      selectedClauseIds: widget.definition.clauses
          .where((c) => _selectedClauseIds.contains(c.id))
          .map((c) => c.id)
          .toList(),
    );
  }

  void _notifyDraftChanged() {
    widget.onDraftChanged?.call(snapshot());
  }

  void setSubmitting(bool value) {
    setState(() => _submitting = value);
  }

  @override
  Widget build(BuildContext context) {
    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (widget.riskNoticeText != null &&
              widget.riskNoticeText!.isNotEmpty) ...[
            WarningBanner(message: widget.riskNoticeText!),
            const SizedBox(height: 8),
          ],
          for (final field in widget.definition.fields) ...[
            _buildField(field),
            const SizedBox(height: 16),
          ],
          if (widget.definition.clauses.isNotEmpty) ...[
            const Divider(),
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 8),
              child: Text(
                'Optional clauses',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            ),
            for (final clause in widget.definition.clauses)
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(clause.title),
                subtitle: clause.text.isEmpty
                    ? null
                    : Text(
                        clause.text,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                value: _selectedClauseIds.contains(clause.id),
                onChanged: _submitting
                    ? null
                    : (value) {
                        setState(() {
                          if (value ?? false) {
                            _selectedClauseIds.add(clause.id);
                          } else {
                            _selectedClauseIds.remove(clause.id);
                          }
                        });
                        _notifyDraftChanged();
                      },
              ),
          ],
        ],
      ),
    );
  }

  Widget _buildField(FieldDefinition field) {
    switch (field.type) {
      case FieldType.date:
        return _DateField(
          field: field,
          controller: _controllers[field.name]!,
          enabled: !_submitting,
          onChanged: _notifyDraftChanged,
        );
      case FieldType.number:
      case FieldType.currency:
        return _NumberField(
          field: field,
          controller: _controllers[field.name]!,
          isCurrency: field.type == FieldType.currency,
          enabled: !_submitting,
          onChanged: (_) => _notifyDraftChanged(),
        );
      case FieldType.text:
        return TextFormField(
          controller: _controllers[field.name],
          enabled: !_submitting,
          decoration: InputDecoration(
            labelText: field.label,
            prefixText: field.required ? '* ' : null,
          ),
          validator: (value) => _validateField(field, value),
          onChanged: (_) => _notifyDraftChanged(),
        );
    }
  }

  String? _validateField(FieldDefinition field, String? raw) {
    final value = raw?.trim() ?? '';
    final rule = field.validation;

    if (value.isEmpty) {
      return field.required ? '${field.label} is required.' : null;
    }

    switch (field.type) {
      case FieldType.text:
        if (rule?.minLength != null && value.length < rule!.minLength!) {
          return '${field.label} must be at least ${rule.minLength} characters.';
        }
        if (rule?.maxLength != null && value.length > rule!.maxLength!) {
          return '${field.label} must be at most ${rule.maxLength} characters.';
        }
        if (rule?.pattern != null &&
            value.isNotEmpty &&
            !RegExp(rule!.pattern!).hasMatch(value)) {
          return '${field.label} has an invalid format.';
        }
        return null;
      case FieldType.number:
      case FieldType.currency:
        final numValue = num.tryParse(value);
        if (numValue == null) {
          return '${field.label} must be a number.';
        }
        if (rule?.nonNegative == true && numValue < 0) {
          return '${field.label} must not be negative.';
        }
        if (rule?.min != null && numValue < rule!.min!) {
          return '${field.label} must be at least ${rule.min}.';
        }
        if (rule?.max != null && numValue > rule!.max!) {
          return '${field.label} must be at most ${rule.max}.';
        }
        return null;
      case FieldType.date:
        final date = DateTime.tryParse(value);
        if (date == null) {
          return '${field.label} must be a valid date (yyyy-MM-dd).';
        }
        if (rule?.dateFrom != null) {
          final from = DateTime.tryParse(rule!.dateFrom!);
          if (from != null && date.isBefore(from)) {
            return '${field.label} must not be before ${rule.dateFrom}.';
          }
        }
        if (rule?.dateTo != null) {
          final to = DateTime.tryParse(rule!.dateTo!);
          if (to != null && date.isAfter(to)) {
            return '${field.label} must not be after ${rule.dateTo}.';
          }
        }
        return null;
    }
  }
}

class _NumberField extends StatelessWidget {
  const _NumberField({
    required this.field,
    required this.controller,
    required this.isCurrency,
    required this.enabled,
    required this.onChanged,
  });

  final FieldDefinition field;
  final TextEditingController controller;
  final bool isCurrency;
  final bool enabled;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      enabled: enabled,
      keyboardType: const TextInputType.numberWithOptions(
        decimal: true,
        signed: true,
      ),
      decoration: InputDecoration(
        labelText: field.label,
        prefixText: isCurrency ? '¥ ' : (field.required ? '* ' : null),
      ),
      validator: (value) => _validateNumber(field, value),
      onChanged: onChanged,
    );
  }

  String? _validateNumber(FieldDefinition field, String? raw) {
    final value = raw?.trim() ?? '';
    if (value.isEmpty) {
      return field.required ? '${field.label} is required.' : null;
    }
    final numValue = num.tryParse(value);
    if (numValue == null) {
      return '${field.label} must be a number.';
    }
    final rule = field.validation;
    if (rule?.nonNegative == true && numValue < 0) {
      return '${field.label} must not be negative.';
    }
    if (rule?.min != null && numValue < rule!.min!) {
      return '${field.label} must be at least ${rule.min}.';
    }
    if (rule?.max != null && numValue > rule!.max!) {
      return '${field.label} must be at most ${rule.max}.';
    }
    return null;
  }
}

class _DateField extends StatefulWidget {
  const _DateField({
    required this.field,
    required this.controller,
    required this.enabled,
    required this.onChanged,
  });

  final FieldDefinition field;
  final TextEditingController controller;
  final bool enabled;
  final VoidCallback onChanged;

  @override
  State<_DateField> createState() => _DateFieldState();
}

class _DateFieldState extends State<_DateField> {
  Future<void> _pick(BuildContext context) async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.tryParse(widget.controller.text) ?? now,
      firstDate: DateTime(1900),
      lastDate: DateTime(2100),
    );
    if (picked != null) {
      final formatted =
          '${picked.year.toString().padLeft(4, '0')}-'
          '${picked.month.toString().padLeft(2, '0')}-'
          '${picked.day.toString().padLeft(2, '0')}';
      widget.controller.text = formatted;
      widget.onChanged();
    }
  }

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: widget.controller,
      readOnly: true,
      enabled: widget.enabled,
      onTap: () => _pick(context),
      decoration: InputDecoration(
        labelText: widget.field.label,
        prefixText: widget.field.required ? '* ' : null,
        suffixIcon: const Icon(Icons.calendar_today_outlined),
      ),
      validator: (value) => _validateDate(widget.field, value),
    );
  }

  String? _validateDate(FieldDefinition field, String? raw) {
    final value = raw?.trim() ?? '';
    if (value.isEmpty) {
      return field.required ? '${field.label} is required.' : null;
    }
    final date = DateTime.tryParse(value);
    if (date == null) {
      return '${field.label} must be a valid date.';
    }
    final rule = field.validation;
    if (rule?.dateFrom != null) {
      final from = DateTime.tryParse(rule!.dateFrom!);
      if (from != null && date.isBefore(from)) {
        return '${field.label} must not be before ${rule.dateFrom}.';
      }
    }
    if (rule?.dateTo != null) {
      final to = DateTime.tryParse(rule!.dateTo!);
      if (to != null && date.isAfter(to)) {
        return '${field.label} must not be after ${rule.dateTo}.';
      }
    }
    return null;
  }
}
