
/// DTOs for the `/api/integrations` management surface. Secrets and clear
/// tokens appear only in creation/rotation responses and are never persisted.
library;

class ServiceTokenView {
  const ServiceTokenView({
    required this.id,
    required this.name,
    required this.prefix,
    required this.scopes,
    required this.createdAtUtc,
    required this.expiresAtUtc,
    this.revokedAtUtc,
    this.lastUsedAtUtc,
    required this.useCount,
  });

  final String id;
  final String name;
  final String prefix;
  final List<String> scopes;
  final DateTime createdAtUtc;
  final DateTime expiresAtUtc;
  final DateTime? revokedAtUtc;
  final DateTime? lastUsedAtUtc;
  final int useCount;

  bool get isRevoked => revokedAtUtc != null;
  bool get isExpired => expiresAtUtc.isBefore(DateTime.now().toUtc());

  factory ServiceTokenView.fromJson(Map<String, dynamic> json) => ServiceTokenView(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        prefix: json['prefix']?.toString() ?? '',
        scopes: (json['scopes'] as String? ?? '')
            .split(',')
            .where((s) => s.isNotEmpty)
            .toList(),
        createdAtUtc: DateTime.tryParse(json['createdAtUtc']?.toString() ?? '')?.toUtc() ?? DateTime.now().toUtc(),
        expiresAtUtc: DateTime.tryParse(json['expiresAtUtc']?.toString() ?? '')?.toUtc() ?? DateTime.now().toUtc(),
        revokedAtUtc: json['revokedAtUtc'] == null ? null : DateTime.tryParse(json['revokedAtUtc'].toString())?.toUtc(),
        lastUsedAtUtc: json['lastUsedAtUtc'] == null ? null : DateTime.tryParse(json['lastUsedAtUtc'].toString())?.toUtc(),
        useCount: (json['useCount'] as num?)?.toInt() ?? 0,
      );
}

class ServiceTokenIssuance {
  const ServiceTokenIssuance({required this.token, required this.clearToken});

  final ServiceTokenView token;
  final String clearToken;

  factory ServiceTokenIssuance.fromJson(Map<String, dynamic> json) => ServiceTokenIssuance(
        token: ServiceTokenView.fromJson(json['token'] as Map<String, dynamic>),
        clearToken: json['clearToken']?.toString() ?? '',
      );
}

class WebhookSubscriptionView {
  const WebhookSubscriptionView({
    required this.id,
    required this.url,
    required this.eventTypes,
    required this.isActive,
    required this.createdAtUtc,
  });

  final String id;
  final String url;
  final List<String> eventTypes;
  final bool isActive;
  final DateTime createdAtUtc;

  factory WebhookSubscriptionView.fromJson(Map<String, dynamic> json) => WebhookSubscriptionView(
        id: json['id']?.toString() ?? '',
        url: json['url']?.toString() ?? '',
        eventTypes: (json['eventTypes'] as String? ?? '')
            .split(',')
            .where((s) => s.isNotEmpty)
            .toList(),
        isActive: json['isActive'] == true,
        createdAtUtc: DateTime.tryParse(json['createdAtUtc']?.toString() ?? '')?.toUtc() ?? DateTime.now().toUtc(),
      );
}

class SubscriptionCreated {
  const SubscriptionCreated({required this.subscription, required this.secret});

  final WebhookSubscriptionView subscription;
  final String secret;

  factory SubscriptionCreated.fromJson(Map<String, dynamic> json) =>
      SubscriptionCreated(
        subscription: WebhookSubscriptionView.fromJson(json['subscription'] as Map<String, dynamic>),
        secret: json['secret']?.toString() ?? '',
      );
}

class DeliveryAttempt {
  const DeliveryAttempt({required this.atUtc, this.statusCode, this.error});

  final DateTime atUtc;
  final int? statusCode;
  final String? error;

  factory DeliveryAttempt.fromJson(Map<String, dynamic> json) => DeliveryAttempt(
        atUtc: DateTime.tryParse(json['atUtc']?.toString() ?? '')?.toUtc() ?? DateTime.now().toUtc(),
        statusCode: (json['statusCode'] as num?)?.toInt(),
        error: json['error']?.toString(),
      );
}

class WebhookDeliveryView {
  const WebhookDeliveryView({
    required this.id,
    required this.subscriptionId,
    required this.eventId,
    required this.state,
    required this.attemptCount,
    this.lastStatusCode,
    this.lastError,
    this.blockedReason,
    required this.attempts,
  });

  final String id;
  final String subscriptionId;
  final String eventId;
  final String state;
  final int attemptCount;
  final int? lastStatusCode;
  final String? lastError;
  final String? blockedReason;
  final List<DeliveryAttempt> attempts;

  bool get isRetryable =>
      state == 'Exhausted' || state == 'Blocked' || state == 'Delivered';

  factory WebhookDeliveryView.fromJson(Map<String, dynamic> json) => WebhookDeliveryView(
        id: json['id']?.toString() ?? '',
        subscriptionId: json['subscriptionId']?.toString() ?? '',
        eventId: json['eventId']?.toString() ?? '',
        state: json['state']?.toString() ?? '',
        attemptCount: (json['attemptCount'] as num?)?.toInt() ?? 0,
        lastStatusCode: (json['lastStatusCode'] as num?)?.toInt(),
        lastError: json['lastError']?.toString(),
        blockedReason: json['blockedReason']?.toString(),
        attempts: ((json['attempts'] as List?) ?? [])
            .map((e) => DeliveryAttempt.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
