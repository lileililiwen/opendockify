import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../auth/application/session_controller.dart';

/// Account recovery. Start always reports success (enumeration-safe); the
/// server logs the challenge id + code on its console for this self-hosted
/// app. Complete consumes the challenge and sets the new password.
class RecoveryScreen extends ConsumerStatefulWidget {
  const RecoveryScreen({super.key});

  @override
  ConsumerState<RecoveryScreen> createState() => _RecoveryScreenState();
}

class _RecoveryScreenState extends ConsumerState<RecoveryScreen> {
  final _username = TextEditingController();
  final _challenge = TextEditingController();
  final _code = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  bool _started = false;
  String? _error;
  String? _notice;

  @override
  void dispose() {
    _username.dispose();
    _challenge.dispose();
    _code.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _start() async {
    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });
    final error = await ref.read(sessionControllerProvider.notifier).recoveryStart(_username.text);
    if (!mounted) return;
    setState(() {
      _busy = false;
      if (error == null) {
        _started = true;
        _notice = 'If the account exists, a recovery challenge was issued. '
            'Find the challenge id and code in the server log, then complete the reset below.';
      } else {
        _error = error;
      }
    });
  }

  Future<void> _complete() async {
    if (_password.text.length < 8) {
      setState(() => _error = 'The new password must be at least 8 characters.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    final error = await ref
        .read(sessionControllerProvider.notifier)
        .recoveryComplete(_challenge.text, _code.text, _password.text);
    if (!mounted) return;
    setState(() {
      _busy = false;
      _error = error;
    });
    if (error == null && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Password reset. Please sign in.')),
      );
      context.go('/login');
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Reset password')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextField(
              controller: _username,
              autocorrect: false,
              decoration: const InputDecoration(labelText: 'Username'),
            ),
            const SizedBox(height: 16),
            FilledButton.tonal(
              onPressed: _busy ? null : _start,
              child: const Text('Send recovery challenge'),
            ),
            if (_notice != null) ...[
              const SizedBox(height: 12),
              Text(_notice!),
            ],
            if (_started) ...[
              const SizedBox(height: 24),
              const Divider(),
              const SizedBox(height: 8),
              TextField(
                controller: _challenge,
                autocorrect: false,
                decoration: const InputDecoration(labelText: 'Challenge id (from server log)'),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _code,
                autocorrect: false,
                decoration: const InputDecoration(labelText: 'Recovery code'),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _password,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'New password'),
                onSubmitted: (_) => _complete(),
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            if (_started) ...[
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _busy ? null : _complete,
                child: _busy
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Reset password'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
