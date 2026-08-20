import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/api/api_error.dart';
import '../../../core/models/admin.dart';
import '../../../core/providers.dart';

/// Loads the allowlisted system settings (secrets masked) and updates them.
class AdminSettingsController extends AsyncNotifier<List<SettingView>> {
  @override
  Future<List<SettingView>> build() {
    return ref.read(apiClientProvider).getSettings();
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() => ref.read(apiClientProvider).getSettings());
  }

  /// Updates a setting value. Returns an error message or null on success.
  Future<String?> updateSetting(String key, String value) async {
    try {
      await ref.read(apiClientProvider).updateSetting(key, value);
      await refresh();
      return null;
    } on ApiError catch (e) {
      return e.message;
    }
  }
}

final adminSettingsControllerProvider =
    AsyncNotifierProvider<AdminSettingsController, List<SettingView>>(AdminSettingsController.new);

/// Loads recent AI usage entries (admin only).
class AiUsageController extends AsyncNotifier<List<AiUsageEntry>> {
  @override
  Future<List<AiUsageEntry>> build() {
    return ref.read(apiClientProvider).listAiUsage(limit: 50);
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() => ref.read(apiClientProvider).listAiUsage(limit: 50));
  }
}

final aiUsageControllerProvider = AsyncNotifierProvider<AiUsageController, List<AiUsageEntry>>(AiUsageController.new);