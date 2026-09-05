import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/models/document.dart';
import '../../../core/providers.dart';
import '../../../core/errors/error_presenter.dart';

/// Paginated document list for the current user.
class DocumentsController extends AsyncNotifier<DocumentListPage> {
  static const int pageSize = 20;

  int _page = 1;
  String _search = '';
  String _archive = 'active';
  String _sort = 'newest';

  @override
  Future<DocumentListPage> build() => _load(1);

  Future<DocumentListPage> _load(int page) async {
    _page = page;
    return ref
        .read(apiClientProvider)
        .listDocuments(
          page: page,
          pageSize: pageSize,
          search: _search,
          archive: _archive,
          sort: _sort,
        );
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
      final next = await ref
          .read(apiClientProvider)
          .listDocuments(
            page: current.page + 1,
            pageSize: pageSize,
            search: _search,
            archive: _archive,
            sort: _sort,
          );
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
  String get search => _search;
  String get archive => _archive;
  String get sort => _sort;

  Future<void> setSearch(String value) async {
    final normalized = value.trim();
    if (_search == normalized) return;
    _search = normalized;
    await refresh();
  }

  Future<void> setArchive(String value) async {
    if (_archive == value) return;
    _archive = value;
    await refresh();
  }

  Future<void> setSort(String value) async {
    if (_sort == value) return;
    _sort = value;
    await refresh();
  }
}

final documentsControllerProvider =
    AsyncNotifierProvider<DocumentsController, DocumentListPage>(
      DocumentsController.new,
    );

/// Loads a single document view by id.
class DocumentDetailController extends AsyncNotifier<DocumentView> {
  DocumentDetailController(this.documentId);

  final String documentId;

  @override
  Future<DocumentView> build() {
    return ref.read(apiClientProvider).getDocument(documentId);
  }

  Future<String?> updateMetadata({
    required String title,
    required bool isArchived,
  }) async {
    try {
      final updated = await ref
          .read(apiClientProvider)
          .updateDocumentMetadata(
            documentId,
            title: title,
            isArchived: isArchived,
          );
      state = AsyncValue.data(updated);
      ref.invalidate(documentsControllerProvider);
      ref.invalidate(documentVersionsProvider(documentId));
      return null;
    } catch (error) {
      return safeErrorMessage(error);
    }
  }
}

final documentDetailControllerProvider =
    AsyncNotifierProvider.family<
      DocumentDetailController,
      DocumentView,
      String
    >(DocumentDetailController.new);

final documentVersionsProvider =
    FutureProvider.family<List<DocumentSummary>, String>((ref, documentId) {
      return ref.read(apiClientProvider).getDocumentVersions(documentId);
    });
