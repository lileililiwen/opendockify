import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

/// One-time secret reveal and destructive-action confirmation helpers.
/// Shared by the integrations screens and their widget tests.
void showSecretOnce(BuildContext context, String title, String secret) {
  showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (dialogContext) => AlertDialog(
      title: Text(title),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Copy it now — it will not be shown again.'),
          const SizedBox(height: 12),
          SelectableText(secret, style: const TextStyle(fontFamily: 'monospace')),
        ],
      ),
      actions: [
        TextButton(
          onPressed: () {
            Clipboard.setData(ClipboardData(text: secret));
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Copied to clipboard.')),
            );
          },
          child: const Text('Copy'),
        ),
        FilledButton(
          onPressed: () => Navigator.of(dialogContext).pop(),
          child: const Text('Done'),
        ),
      ],
    ),
  );
}

Future<bool> confirmAction(BuildContext context, String message) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: const Text('Are you sure?'),
      content: Text(message),
      actions: [
        TextButton(onPressed: () => Navigator.of(dialogContext).pop(false), child: const Text('Cancel')),
        FilledButton(onPressed: () => Navigator.of(dialogContext).pop(true), child: const Text('Confirm')),
      ],
    ),
  );
  return confirmed == true;
}

