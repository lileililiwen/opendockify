enum ApiErrorKind {
  network,
  unauthorized,
  forbidden,
  notFound,
  validation,
  rateLimited,
  aiDisabled,
  server,
  unknown,
}

class ApiError implements Exception {
  const ApiError(this.kind, this.message, {this.statusCode, this.code, this.correlationId});

  final ApiErrorKind kind;
  final String message;
  final int? statusCode;

  /// Stable error code returned by the API (RFC9457 `code` extension), e.g.
  /// `rate_limited`, `validation_failed`, `unauthorized`. `null` when the
  /// server did not include a code (e.g. transport errors).
  final String? code;

  /// `X-Correlation-Id` echoed by the server on the response that produced
  /// the error. Surface it to the user when asking for support.
  final String? correlationId;

  bool get isUnauthorized => kind == ApiErrorKind.unauthorized;
  bool get isForbidden => kind == ApiErrorKind.forbidden;

  @override
  String toString() {
    final parts = <String>['ApiError(${kind.name}'];
    if (statusCode != null) parts.add(statusCode.toString());
    if (code != null) parts.add('code=$code');
    parts.add(message);
    if (correlationId != null) parts.add('correlationId=$correlationId');
    return '${parts.join(', ')})';
  }
}