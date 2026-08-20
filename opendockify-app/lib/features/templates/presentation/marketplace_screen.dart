import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../../core/legal/disclaimer_dialog.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';
import '../../../core/widgets/common.dart';
import '../../settings/presentation/main_navigation_bar.dart';
import '../application/marketplace_controller.dart';

class MarketplaceScreen extends ConsumerStatefulWidget {
  const MarketplaceScreen({super.key});

  @override
  ConsumerState<MarketplaceScreen> createState() => _MarketplaceScreenState();
}

class _MarketplaceScreenState extends ConsumerState<MarketplaceScreen> {
  bool _disclaimerChecked = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _ensureDisclaimer());
  }

  Future<void> _ensureDisclaimer() async {
    if (_disclaimerChecked) return;
    _disclaimerChecked = true;
    final store = ref.read(appConfigStoreProvider);
    final seen = await store.readDisclaimerSeen();
    if (!seen && mounted) {
      await DisclaimerDialog.showIfNeeded(context);
      await store.writeDisclaimerSeen();
    }
  }

  @override
  Widget build(BuildContext context) {
    final marketplace = ref.watch(marketplaceControllerProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Templates'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.read(marketplaceControllerProvider.notifier).refresh(),
            tooltip: 'Refresh',
          ),
        ],
      ),
      body: marketplace.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => ErrorView(
          message: error.toString(),
          onRetry: () => ref.read(marketplaceControllerProvider.notifier).refresh(),
        ),
        data: (templates) {
          if (templates.isEmpty) {
            return const Center(child: Text('No templates available.'));
          }
          final categories = <String, List<TemplateSummary>>{};
          for (final t in templates) {
            categories.putIfAbsent(t.category, () => []).add(t);
          }
          return RefreshIndicator(
            onRefresh: () => ref.read(marketplaceControllerProvider.notifier).refresh(),
            child: ListView(
              children: [
                for (final entry in categories.entries) ...[
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
                    child: Text(
                      entry.key.isEmpty ? 'Uncategorized' : entry.key,
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  for (final t in entry.value)
                    ListTile(
                      leading: Icon(t.isBuiltIn ? Icons.auto_awesome : Icons.description_outlined),
                      title: Text(t.name),
                      subtitle: t.description.isEmpty ? null : Text(t.description, maxLines: 2, overflow: TextOverflow.ellipsis),
                      trailing: t.isBuiltIn ? const Text('built-in') : null,
                      onTap: () => context.push('/templates/${t.id}'),
                    ),
                ],
              ],
            ),
          );
        },
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => context.push('/templates/new'),
        icon: const Icon(Icons.add),
        label: const Text('New template'),
      ),
      bottomNavigationBar: const MainNavigationBar(selectedIndex: 0),
    );
  }
}