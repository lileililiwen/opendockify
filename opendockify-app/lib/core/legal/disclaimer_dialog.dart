import 'package:flutter/material.dart';

import 'legal_text.dart';

/// Mandatory legal disclaimer shown on first launch (before any drafting
/// workflow) and from the About screen.
class DisclaimerDialog extends StatelessWidget {
  const DisclaimerDialog({super.key});

  static Future<void> showIfNeeded(BuildContext context) {
    return showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (context) => const DisclaimerDialog(),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Legal disclaimer'),
      content: SingleChildScrollView(
        child: Text(appLegalDisclaimer),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('I understand'),
        ),
      ],
    );
  }
}