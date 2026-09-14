import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../application/session_controller.dart';

/// Second login step: verifies the TOTP code against the pending challenge
/// and completes sign-in.
class TwoFactorVerifyScreen extends ConsumerStatefulWidget {
  const TwoFactorVerifyScreen({super.key});

  @override
  ConsumerState<TwoFactorVerifyScreen> createState() => _TwoFactorVerifyScreenState();
}

class _TwoFactorVerifyScreenState extends ConsumerState<TwoFactorVerifyScreen> {
  final _code = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    final error = await ref.read(sessionControllerProvider.notifier).verifyTwoFactor(_code.text);
    if (!mounted) return;
    setState(() {
      _busy = false;
      _error = error;
    });
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(sessionControllerProvider, (prev, next) {
      if (next.status == SessionStatus.authenticated && prev?.status != SessionStatus.authenticated) {
        context.go('/templates');
      }
    });

    final pendingUser = ref.watch(sessionControllerProvider).pendingUsername;

    return Scaffold(
      appBar: AppBar(title: const Text('Two-factor verification')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (pendingUser != null && pendingUser.isNotEmpty) Text('Signing in as $pendingUser'),
            const SizedBox(height: 8),
            const Text('Enter the 6-digit code from your authenticator app.'),
            const SizedBox(height: 16),
            TextField(
              controller: _code,
              autocorrect: false,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Code'),
              onSubmitted: (_) => _submit(),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: _busy ? null : _submit,
              child: _busy
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Verify'),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: _busy
                  ? null
                  : () {
                      ref.read(sessionControllerProvider.notifier).cancelTwoFactor();
                      context.go('/login');
                    },
              child: const Text('Cancel'),
            ),
          ],
        ),
      ),
    );
  }
}
