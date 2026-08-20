import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/config/connection_controller.dart';

/// First-launch / reset screen for configuring the self-hosted server URL.
class ConnectionScreen extends ConsumerStatefulWidget {
  const ConnectionScreen({super.key});

  @override
  ConsumerState<ConnectionScreen> createState() => _ConnectionScreenState();
}

class _ConnectionScreenState extends ConsumerState<ConnectionScreen> {
  final _controller = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _connect() async {
    setState(() => _error = null);
    final error = await ref.read(connectionControllerProvider.notifier).connect(_controller.text);
    if (!mounted) return;
    if (error != null) {
      setState(() => _error = error);
    }
  }

  @override
  Widget build(BuildContext context) {
    final connection = ref.watch(connectionControllerProvider);
    final hasExisting = connection.baseUrl?.isNotEmpty ?? false;
    return Scaffold(
      appBar: AppBar(title: const Text('Connect to server')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Icon(Icons.dns_outlined, size: 64),
            const SizedBox(height: 16),
            Text(
              'Enter the address of your self-hosted OpenDockify server.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            TextField(
              controller: _controller,
              keyboardType: TextInputType.url,
              decoration: const InputDecoration(
                labelText: 'Server URL',
                hintText: 'http://192.168.1.10:8080',
              ),
              onSubmitted: (_) => _connect(),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
            ],
            const SizedBox(height: 24),
            FilledButton(
              onPressed: connection.isLoading ? null : _connect,
              child: connection.isLoading
                  ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                  : const Text('Connect'),
            ),
            if (hasExisting) ...[
              const SizedBox(height: 12),
              OutlinedButton(
                onPressed: () async {
                  await ref.read(connectionControllerProvider.notifier).reset();
                },
                child: const Text('Reset'),
              ),
            ],
            const SizedBox(height: 24),
            Text(
              'OpenDockify is self-hosted only. The app talks directly to the server you configure — no cloud service is involved.',
              style: Theme.of(context).textTheme.bodySmall,
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}