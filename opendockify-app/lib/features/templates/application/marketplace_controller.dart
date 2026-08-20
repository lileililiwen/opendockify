import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/template_dto.dart';
import '../../../core/providers.dart';

/// Loads the template marketplace and provides copy/delete actions.
class MarketplaceController extends AsyncNotifier<List<TemplateSummary>> {
  @override
  Future<List<TemplateSummary>> build() => _load();

  Future<List<TemplateSummary>> _load() async {
    final items = await ref.read(apiClientProvider).listMarketplace();
    return items;
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(_load);
  }

  Future<String?> copy(String templateId) async {
    try {
      await ref.read(apiClientProvider).copyTemplate(templateId);
      await refresh();
      return null;
    } on ApiError catch (e) {
      return e.message;
    }
  }

  Future<String?> delete(String templateId) async {
    try {
      await ref.read(apiClientProvider).deleteTemplate(templateId);
      await refresh();
      return null;
    } on ApiError catch (e) {
      return e.message;
    }
  }
}

final marketplaceControllerProvider = AsyncNotifierProvider<MarketplaceController, List<TemplateSummary>>(MarketplaceController.new);