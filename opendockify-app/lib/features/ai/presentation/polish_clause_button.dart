import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/ai.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';
import 'ai_consent.dart';

/// Polishes a user-drafted clause via the AI endpoint. Opens a dialog where
/// the user enters their draft; the polished text can be accepted (copied) or
/// discarded.
class PolishClauseButton extends ConsumerStatefulWidget {
  const PolishClauseButton({super.key, required this.templateId});

  final String templateId;

  @override
  ConsumerState<PolishClauseButton> createState() => _PolishClauseButtonState();
}

class _PolishClauseButtonState extends ConsumerState<PolishClauseButton> {
  bool _busy = false;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      onPressed: _busy ? null : _openDraftDialog,
      icon: const Icon(Icons.auto_fix_high),
      label: const Text('Polish a clause (AI)'),
    );
  }

  Future<void> _openDraftDialog() async {
    final draftController = TextEditingController();
    final messenger = ScaffoldMessenger.of(context);
    final entered = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Polish a clause'),
        content: TextField(
          controller: draftController,
          autofocus: true,
          maxLines: 6,
          decoration: const InputDecoration(labelText: 'Your draft clause'),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.of(context).pop(draftController.text), child: const Text('Polish')),
        ],
      ),
    );
    draftController.dispose();
    if (entered == null || entered.trim().isEmpty) return;
    if (!mounted) return;

    if (!await ensureAiConsent(context)) return;

    setState(() => _busy = true);
    try {
      final request = PolishClauseRequest(templateId: widget.templateId, draft: entered);
      final PolishResult result = await ref.read(apiClientProvider).polishClause(request);
      if (!mounted) return;
      setState(() => _busy = false);
      await _showResult(context, result.text, result.warning);
    } on ApiError catch (e) {
      if (!mounted) return;
      setState(() => _busy = false);
      messenger.showSnackBar(SnackBar(content: Text(_friendlyMessage(e))));
    }
  }

  String _friendlyMessage(ApiError e) {
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
      builder: (dialogContext) => AlertDialog(
        title: const Text('Polished clause'),
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
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Discard'),
          ),
          FilledButton(
            onPressed: () {
              // Copy the polished text to the clipboard so the user can use it.
              final messenger = ScaffoldMessenger.of(context);
              Navigator.of(dialogContext).pop();
              messenger.showSnackBar(const SnackBar(content: Text('Polished text is shown above — copy it to use it.')));
            },
            child: const Text('Done'),
          ),
        ],
      ),
    );
  }
}