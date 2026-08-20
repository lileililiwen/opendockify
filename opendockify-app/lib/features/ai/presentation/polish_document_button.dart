import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/ai.dart';
import '../../../core/models/document.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';
import 'ai_consent.dart';

/// Polishes the full rendered document via the AI endpoint. Shows the privacy
/// warning first and handles 403 (AI disabled) / 429 (rate limit).
class PolishDocumentButton extends ConsumerStatefulWidget {
  const PolishDocumentButton({super.key, required this.document});

  final DocumentView document;

  @override
  ConsumerState<PolishDocumentButton> createState() => _PolishDocumentButtonState();
}

class _PolishDocumentButtonState extends ConsumerState<PolishDocumentButton> {
  bool _busy = false;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: _busy ? null : _polish,
      icon: _busy
          ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2))
          : const Icon(Icons.auto_fix_high),
      label: const Text('Polish document (AI)'),
    );
  }

  Future<void> _polish() async {
    final messenger = ScaffoldMessenger.of(context);
    if (!await ensureAiConsent(context)) return;
    final doc = widget.document;
    final templateId = doc.templateId;

    final request = PolishDocumentRequest(
      templateId: templateId,
      renderedText: doc.renderedText,
    );

    setState(() => _busy = true);
    try {
      final result = await ref.read(apiClientProvider).polishDocument(request);
      if (!mounted) return;
      setState(() => _busy = false);
      await _showResult(context, result.text, result.warning);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() => _busy = false);
      messenger.showSnackBar(SnackBar(content: Text(friendlyMessage(e))));
    }
  }

  String friendlyMessage(ApiError e) {
    switch (e.kind) {
      case ApiErrorKind.aiDisabled:
        return 'AI is disabled by the administrator.';
      case ApiErrorKind.rateLimited:
        return 'Daily AI usage limit reached.';
      default:
        return e.message.isEmpty ? 'AI polish failed. Please try again.' : e.message;
    }
  }

  Future<void> _showResult(BuildContext context, String text, String warning) async {
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Polished document'),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              if (warning.isNotEmpty) WarningBanner(message: warning),
              const SizedBox(height: 8),
              SelectableText(text.isEmpty ? '(no text)' : text),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Discard'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('OK'),
          ),
        ],
      ),
    );
  }
}