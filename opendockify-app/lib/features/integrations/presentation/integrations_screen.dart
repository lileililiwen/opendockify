import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../auth/application/session_controller.dart';
import '../providers.dart';
import 'integration_dialogs.dart';

/// Owner-facing management of service tokens, webhook subscriptions, and
/// delivery diagnostics. Secrets/clear tokens are shown exactly once via a
/// dismissible dialog with a copy affordance; lists never contain them.
class IntegrationsScreen extends ConsumerWidget {
  const IntegrationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider);
    if (!session.isAuthenticated) {
      return const Scaffold(body: Center(child: Text('Sign in required.')));
    }

    return DefaultTabController(
      length: 3,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Integrations'),
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Tokens'),
              Tab(text: 'Webhooks'),
              Tab(text: 'Deliveries'),
            ],
          ),
        ),
        body: const TabBarView(
          children: [
            _TokensTab(),
            _SubscriptionsTab(),
            _DeliveriesTab(),
          ],
        ),
      ),
    );
  }
}

class _TokensTab extends ConsumerStatefulWidget {
  const _TokensTab();

  @override
  ConsumerState<_TokensTab> createState() => _TokensTabState();
}

class _TokensTabState extends ConsumerState<_TokensTab> {
  @override
  Widget build(BuildContext context) {
    final state = ref.watch(tokensControllerProvider);
    final tokens = state.items;

    return Stack(
      children: [
        RefreshIndicator(
          onRefresh: () => ref.read(tokensControllerProvider.notifier).refresh(),
          child: state.loading && tokens.isEmpty
              ? const Center(child: CircularProgressIndicator())
              : ListView(
                  children: [
                    if (state.error != null)
                      Padding(
                        padding: const EdgeInsets.all(16),
                        child: Text(state.error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                      ),
                    for (final token in tokens)
                      ListTile(
                        leading: Icon(
                          token.isRevoked || token.isExpired ? Icons.block : Icons.vpn_key_outlined,
                        ),
                        title: Text(token.name),
                        subtitle: Text(
                          '${token.prefix}… · ${token.scopes.join(', ')}\n'
                          'Expires ${_date(token.expiresAtUtc)} · used ${token.useCount}×',
                        ),
                        isThreeLine: true,
                        trailing: token.isRevoked || token.isExpired
                            ? null
                            : IconButton(
                                icon: const Icon(Icons.delete_outline),
                                tooltip: 'Revoke',
                                onPressed: () async {
                                  final ok = await confirmAction(
                                    context,
                                    "Revoke token '${token.name}'? Clients using it stop working immediately.",
                                  );
                                  if (ok) await ref.read(tokensControllerProvider.notifier).revoke(token.id);
                                },
                              ),
                      ),
                    if (tokens.isEmpty && !state.loading)
                      const Padding(
                        padding: EdgeInsets.all(16),
                        child: Text('No service tokens yet.'),
                      ),
                  ],
                ),
        ),
        Positioned(
          right: 16,
          bottom: 16,
          child: FloatingActionButton.extended(
            heroTag: 'create-token',
            icon: const Icon(Icons.add),
            label: const Text('New token'),
            onPressed: () => _createTokenFlow(context, ref),
          ),
        ),
      ],
    );
  }

  Future<void> _createTokenFlow(BuildContext context, WidgetRef ref) async {
    final nameController = TextEditingController();
    final scopes = {
      for (final scope in IntegrationScopes.all) scope: false,
    };
    var expiresInDays = 90;

    final created = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => StatefulBuilder(
        builder: (sheetContext, setSheetState) => Padding(
          padding: EdgeInsets.only(
            left: 16,
            right: 16,
            top: 16,
            bottom: MediaQuery.of(sheetContext).viewInsets.bottom + 16,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              TextField(
                controller: nameController,
                decoration: const InputDecoration(labelText: 'Token name'),
              ),
              const SizedBox(height: 8),
              const Text('Scopes', style: TextStyle(fontWeight: FontWeight.bold)),
              ...scopes.keys.map(
                (scope) => CheckboxListTile(
                  dense: true,
                  title: Text(scope),
                  value: scopes[scope],
                  onChanged: (v) => setSheetState(() => scopes[scope] = v ?? false),
                ),
              ),
              DropdownButtonFormField<int>(
                initialValue: expiresInDays,
                decoration: const InputDecoration(labelText: 'Expires in'),
                items: const [7, 30, 90, 365]
                    .map((d) => DropdownMenuItem(value: d, child: Text('$d days')))
                    .toList(),
                onChanged: (v) => setSheetState(() => expiresInDays = v ?? 90),
              ),
              const SizedBox(height: 12),
              FilledButton(
                onPressed: () => Navigator.of(sheetContext).pop(true),
                child: const Text('Create'),
              ),
            ],
          ),
        ),
      ),
    );

    if (created != true) return;
    final selected = scopes.entries.where((e) => e.value).map((e) => e.key).toList();
    if (selected.isEmpty || nameController.text.trim().isEmpty) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('A name and at least one scope are required.')),
        );
      }
      return;
    }

    final issuance = await ref.read(tokensControllerProvider.notifier).create(
          name: nameController.text.trim(),
          scopes: selected,
          expiresInDays: expiresInDays,
        );
    if (issuance != null && context.mounted) {
      showSecretOnce(context, 'Service token created', issuance.clearToken);
    }
  }
}

class _SubscriptionsTab extends ConsumerWidget {
  const _SubscriptionsTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(subscriptionsControllerProvider);
    final urlController = TextEditingController();

    return Stack(
      children: [
        RefreshIndicator(
          onRefresh: () => ref.read(subscriptionsControllerProvider.notifier).refresh(),
          child: state.loading && state.items.isEmpty
              ? const Center(child: CircularProgressIndicator())
              : ListView(
                  children: [
                    if (state.error != null)
                      Padding(
                        padding: const EdgeInsets.all(16),
                        child: Text(state.error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                      ),
                    for (final sub in state.items)
                      ListTile(
                        leading: Icon(
                          sub.isActive ? Icons.webhook : Icons.webhook_outlined,
                          color: sub.isActive ? null : Theme.of(context).disabledColor,
                        ),
                        title: Text(sub.url),
                        subtitle: Text('${sub.eventTypes.join(', ')}\nCreated ${_date(sub.createdAtUtc)}'),
                        isThreeLine: true,
                        trailing: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            IconButton(
                              icon: const Icon(Icons.refresh),
                              tooltip: 'Rotate secret',
                              onPressed: () async {
                                final ok = await confirmAction(
                                  context,
                                  'Rotate the signing secret? The old secret stops working immediately.',
                                );
                                if (!ok) return;
                                final secret = await ref
                                    .read(subscriptionsControllerProvider.notifier)
                                    .rotateSecret(sub.id);
                                if (secret != null && context.mounted) {
                                  showSecretOnce(context, 'New webhook secret', secret);
                                }
                              },
                            ),
                            IconButton(
                              icon: const Icon(Icons.delete_outline),
                              tooltip: 'Delete',
                              onPressed: () async {
                                final ok = await confirmAction(context, 'Delete this subscription?');
                                if (ok) await ref.read(subscriptionsControllerProvider.notifier).delete(sub.id);
                              },
                            ),
                          ],
                        ),
                      ),
                    if (state.items.isEmpty && !state.loading)
                      const Padding(
                        padding: EdgeInsets.all(16),
                        child: Text('No webhook subscriptions yet.'),
                      ),
                  ],
                ),
        ),
        Positioned(
          right: 16,
          bottom: 16,
          child: FloatingActionButton.extended(
            heroTag: 'create-subscription',
            icon: const Icon(Icons.add),
            label: const Text('New webhook'),
            onPressed: () async {
              final created = await showModalBottomSheet<bool>(
                context: context,
                isScrollControlled: true,
                builder: (sheetContext) => Padding(
                  padding: EdgeInsets.only(
                    left: 16,
                    right: 16,
                    top: 16,
                    bottom: MediaQuery.of(sheetContext).viewInsets.bottom + 16,
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      TextField(
                        controller: urlController,
                        keyboardType: TextInputType.url,
                        decoration: const InputDecoration(labelText: 'HTTPS endpoint URL'),
                      ),
                      const SizedBox(height: 8),
                      const Align(
                        alignment: Alignment.centerLeft,
                        child: Text('Events: document.finalized'),
                      ),
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: () => Navigator.of(sheetContext).pop(true),
                        child: const Text('Create'),
                      ),
                    ],
                  ),
                ),
              );
              if (created == true && urlController.text.trim().isNotEmpty) {
                final result = await ref
                    .read(subscriptionsControllerProvider.notifier)
                    .create(url: urlController.text.trim(), eventTypes: ['document.finalized']);
                if (result != null && context.mounted) {
                  showSecretOnce(context, 'Webhook secret', result.secret);
                }
              }
            },
          ),
        ),
      ],
    );
  }
}

class _DeliveriesTab extends ConsumerStatefulWidget {
  const _DeliveriesTab();

  @override
  ConsumerState<_DeliveriesTab> createState() => _DeliveriesTabState();
}

class _DeliveriesTabState extends ConsumerState<_DeliveriesTab> {
  String? _subscriptionId;

  @override
  Widget build(BuildContext context) {
    final subs = ref.watch(subscriptionsControllerProvider);
    final deliveries = ref.watch(deliveriesControllerProvider);

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: DropdownButtonFormField<String?>(
            initialValue: _subscriptionId,
            decoration: const InputDecoration(labelText: 'Subscription'),
            items: [
              const DropdownMenuItem(value: null, child: Text('All subscriptions')),
              ...subs.items.map(
                (s) => DropdownMenuItem(value: s.id, child: Text(s.url, overflow: TextOverflow.ellipsis)),
              ),
            ],
            onChanged: (value) {
              setState(() => _subscriptionId = value);
              ref.read(deliveriesControllerProvider.notifier).refresh(subscriptionId: value);
            },
          ),
        ),
        Expanded(
          child: RefreshIndicator(
            onRefresh: () =>
                ref.read(deliveriesControllerProvider.notifier).refresh(subscriptionId: _subscriptionId),
            child: deliveries.loading && deliveries.items.isEmpty
                ? const Center(child: CircularProgressIndicator())
                : ListView(
                    children: [
                      if (deliveries.error != null)
                        Padding(
                          padding: const EdgeInsets.all(16),
                          child:
                              Text(deliveries.error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                        ),
                      for (final delivery in deliveries.items)
                        ExpansionTile(
                          leading: _stateIcon(delivery.state),
                          title: Text('${delivery.state} · ${delivery.attemptCount} attempts'),
                          subtitle: Text(
                            delivery.lastError ??
                                (delivery.blockedReason != null ? 'blocked: ${delivery.blockedReason}' : '') +
                                    (delivery.lastStatusCode != null ? ' · HTTP ${delivery.lastStatusCode}' : ''),
                          ),
                          children: [
                            for (final attempt in delivery.attempts)
                              ListTile(
                                dense: true,
                                leading: const Icon(Icons.history, size: 18),
                                title: Text(
                                  '${attempt.statusCode?.toString() ?? attempt.error ?? '-'} at ${_date(attempt.atUtc)}',
                                ),
                              ),
                            if (delivery.isRetryable)
                              TextButton.icon(
                                icon: const Icon(Icons.replay),
                                label: const Text('Retry now'),
                                onPressed: () async {
                                  final ok = await ref
                                      .read(deliveriesControllerProvider.notifier)
                                      .retry(delivery.id);
                                  if (ok && context.mounted) {
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(content: Text('Delivery reset to pending.')),
                                    );
                                  }
                                },
                              ),
                          ],
                        ),
                      if (deliveries.items.isEmpty && !deliveries.loading)
                        const Padding(
                          padding: EdgeInsets.all(16),
                          child: Text('No deliveries yet. Finalize a document to trigger one.'),
                        ),
                    ],
                  ),
          ),
        ),
      ],
    );
  }
}

Icon _stateIcon(String state) {
  switch (state) {
    case 'Delivered':
      return const Icon(Icons.check_circle, color: Colors.green);
    case 'Pending':
    case 'Delivering':
      return const Icon(Icons.schedule);
    case 'Exhausted':
      return const Icon(Icons.error_outline, color: Colors.orange);
    default:
      return const Icon(Icons.block, color: Colors.red);
  }
}

String _date(DateTime utc) {
  final local = utc.toLocal();
  return '${local.year}-${local.month.toString().padLeft(2, '0')}-${local.day.toString().padLeft(2, '0')} '
      '${local.hour.toString().padLeft(2, '0')}:${local.minute.toString().padLeft(2, '0')}';
}
