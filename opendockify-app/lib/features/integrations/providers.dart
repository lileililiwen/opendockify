import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_error.dart';
import '../../core/models/integrations.dart';
import '../../core/providers.dart';

/// Scope names mirrored from the server's closed set.
class IntegrationScopes {
  static const templatesRead = 'templates:read';
  static const documentsRead = 'documents:read';
  static const documentsPreview = 'documents:preview';
  static const documentsWrite = 'documents:write';
  static const operationsRead = 'operations:read';

  static const all = [templatesRead, documentsRead, documentsPreview, documentsWrite, operationsRead];
}

/// Controllers for the Integrations screen. Each list is refreshed explicitly;
/// no background polling in this iteration.

class TokensState {
  const TokensState({this.loading = false, this.items = const [], this.error});

  final bool loading;
  final List<ServiceTokenView> items;
  final String? error;

  TokensState copyWith({bool? loading, List<ServiceTokenView>? items, String? error}) =>
      TokensState(
        loading: loading ?? this.loading,
        items: items ?? this.items,
        error: error,
      );
}

class TokensController extends Notifier<TokensState> {
  @override
  TokensState build() {
    Future.microtask(refresh);
    return const TokensState();
  }

  Future<void> refresh() async {
    state = state.copyWith(loading: true, error: null);
    try {
      state = TokensState(items: await ref.read(apiClientProvider).listTokens());
    } on ApiError catch (e) {
      state = state.copyWith(loading: false, error: e.message);
    }
  }

  Future<ServiceTokenIssuance?> create({
    required String name,
    required List<String> scopes,
    required int expiresInDays,
  }) async {
    try {
      final issuance = await ref.read(apiClientProvider)
          .createToken(name: name, scopes: scopes, expiresInDays: expiresInDays);
      await refresh();
      return issuance;
    } on ApiError catch (e) {
      state = state.copyWith(error: e.message);
      return null;
    }
  }

  Future<bool> revoke(String id) async {
    try {
      await ref.read(apiClientProvider).revokeToken(id);
      await refresh();
      return true;
    } on ApiError catch (e) {
      state = state.copyWith(error: e.message);
      return false;
    }
  }
}

final tokensControllerProvider =
    NotifierProvider<TokensController, TokensState>(TokensController.new);

class SubscriptionsState {
  const SubscriptionsState({this.loading = false, this.items = const [], this.error});

  final bool loading;
  final List<WebhookSubscriptionView> items;
  final String? error;

  SubscriptionsState copyWith(
          {bool? loading, List<WebhookSubscriptionView>? items, String? error}) =>
      SubscriptionsState(
        loading: loading ?? this.loading,
        items: items ?? this.items,
        error: error,
      );
}

class SubscriptionsController extends Notifier<SubscriptionsState> {
  @override
  SubscriptionsState build() {
    Future.microtask(refresh);
    return const SubscriptionsState();
  }

  Future<void> refresh() async {
    state = state.copyWith(loading: true, error: null);
    try {
      state = SubscriptionsState(items: await ref.read(apiClientProvider).listSubscriptions());
    } on ApiError catch (e) {
      state = state.copyWith(loading: false, error: e.message);
    }
  }

  Future<SubscriptionCreated?> create({required String url, required List<String> eventTypes}) async {
    try {
      final created =
          await ref.read(apiClientProvider).createSubscription(url: url, eventTypes: eventTypes);
      await refresh();
      return created;
    } on ApiError catch (e) {
      state = state.copyWith(error: e.message);
      return null;
    }
  }

  Future<String?> rotateSecret(String id) async {
    try {
      final secret = await ref.read(apiClientProvider).rotateSubscriptionSecret(id);
      await refresh();
      return secret;
    } on ApiError catch (e) {
      state = state.copyWith(error: e.message);
      return null;
    }
  }

  Future<bool> delete(String id) async {
    try {
      await ref.read(apiClientProvider).deleteSubscription(id);
      await refresh();
      return true;
    } on ApiError catch (e) {
      state = state.copyWith(error: e.message);
      return false;
    }
  }
}

final subscriptionsControllerProvider =
    NotifierProvider<SubscriptionsController, SubscriptionsState>(
        SubscriptionsController.new);

class DeliveriesState {
  const DeliveriesState({
    this.loading = false,
    this.items = const [],
    this.error,
    this.subscriptionId,
  });

  final bool loading;
  final List<WebhookDeliveryView> items;
  final String? error;
  final String? subscriptionId;

  DeliveriesState copyWith({
    bool? loading,
    List<WebhookDeliveryView>? items,
    String? error,
    String? subscriptionId,
  }) =>
      DeliveriesState(
        loading: loading ?? this.loading,
        items: items ?? this.items,
        error: error,
        subscriptionId: subscriptionId ?? this.subscriptionId,
      );
}

class DeliveriesController extends Notifier<DeliveriesState> {
  @override
  DeliveriesState build() => const DeliveriesState();

  Future<void> refresh({String? subscriptionId}) async {
    state = state.copyWith(loading: true, error: null, subscriptionId: subscriptionId);
    try {
      state = DeliveriesState(
        items:
            await ref.read(apiClientProvider).listDeliveries(subscriptionId: subscriptionId),
        subscriptionId: subscriptionId,
      );
    } on ApiError catch (e) {
      state = state.copyWith(loading: false, error: e.message);
    }
  }

  Future<bool> retry(String id) async {
    try {
      await ref.read(apiClientProvider).retryDelivery(id);
      await refresh(subscriptionId: state.subscriptionId);
      return true;
    } on ApiError catch (e) {
      state = state.copyWith(error: e.message);
      return false;
    }
  }
}

final deliveriesControllerProvider =
    NotifierProvider<DeliveriesController, DeliveriesState>(DeliveriesController.new);
