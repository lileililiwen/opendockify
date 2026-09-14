import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/legal/legal_text.dart';
import '../application/session_controller.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _username = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final session = ref.read(sessionControllerProvider.notifier);
    setState(() {
      _busy = true;
      _error = null;
    });
    final error = await session.login(_username.text.trim(), _password.text);
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
      if (next.needsTwoFactor && prev?.needsTwoFactor != true) {
        context.push('/2fa-verify');
      }
      if (next.notice != null && next.notice!.isNotEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(next.notice!)));
        ref.read(sessionControllerProvider.notifier).clearNotice();
      }
    });

    return Scaffold(
      body: SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 48),
              const Icon(Icons.description_outlined, size: 72),
              const SizedBox(height: 12),
              Text('OpenDockify', textAlign: TextAlign.center, style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 4),
              Text('Sign in to your server', textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium),
              const SizedBox(height: 32),
              TextField(
                controller: _username,
                autocorrect: false,
                decoration: const InputDecoration(labelText: 'Username'),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _password,
                obscureText: true,
                decoration: const InputDecoration(labelText: 'Password'),
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
                    : const Text('Sign in'),
              ),
              const SizedBox(height: 8),
              TextButton(
                onPressed: _busy ? null : () => context.push('/register'),
                child: const Text('Create an account'),
              ),
              TextButton(
                onPressed: _busy ? null : () => context.push('/recovery'),
                child: const Text('Forgot password?'),
              ),
              const SizedBox(height: 24),
              Text(
                appLegalDisclaimer,
                style: Theme.of(context).textTheme.bodySmall,
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}