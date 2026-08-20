import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/document.dart';
import '../../../core/providers.dart';

/// Paginated document list for the current user.
class DocumentsController extends AsyncNotifier<DocumentListPage> {
  static const int pageSize = 20;

  int _page = 1;

  @override
  Future<DocumentListPage> build() => _load(1);

  Future<DocumentListPage> _load(int page) async {
    _page = page;
    return ref.read(apiClientProvider).listDocuments(page: page, pageSize: pageSize);
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    state = await AsyncValue.guard(() => _load(1));
  }

  /// Loads the next page, appending to the current list.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore) return;
    try {
      final next = await ref.read(apiClientProvider).listDocuments(page: current.page + 1, pageSize: pageSize);
      final merged = DocumentListPage(
        items: [...current.items, ...next.items],
        page: next.page,
        pageSize: next.pageSize,
        totalCount: next.totalCount,
        totalPages: next.totalPages,
      );
      state = AsyncValue.data(merged);
    } catch (_) {
      // Keep current data; a later refresh can recover.
    }
  }

  int get currentPage => _page;
}

final documentsControllerProvider = AsyncNotifierProvider<DocumentsController, DocumentListPage>(DocumentsController.new);

/// Loads a single document view by id.
class DocumentDetailController extends AsyncNotifier<DocumentView> {
  DocumentDetailController(this.documentId);

  final String documentId;

  @override
  Future<DocumentView> build() {
    return ref.read(apiClientProvider).getDocument(documentId);
  }
}

final documentDetailControllerProvider =
    AsyncNotifierProvider.family<DocumentDetailController, DocumentView, String>(DocumentDetailController.new);