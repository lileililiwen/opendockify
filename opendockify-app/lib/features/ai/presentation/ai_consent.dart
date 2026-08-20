import 'package:flutter/material.dart';

import '../../../core/legal/legal_text.dart';

/// Shows the mandatory AI privacy/sensitivity warning. Returns true when the
/// user proceeds. Only shown once per app session.
bool _warningShown = false;

Future<bool> ensureAiConsent(BuildContext context) async {
  if (_warningShown) return true;
  final proceed = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('AI privacy warning'),
      content: SingleChildScrollView(child: Text(aiPrivacyWarning)),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Cancel')),
        FilledButton(onPressed: () => Navigator.of(context).pop(true), child: const Text('Continue')),
      ],
    ),
  );
  if (proceed == true) _warningShown = true;
  return proceed == true;
}