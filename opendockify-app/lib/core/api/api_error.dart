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
  const ApiError(this.kind, this.message, {this.statusCode});

  final ApiErrorKind kind;
  final String message;
  final int? statusCode;

  bool get isUnauthorized => kind == ApiErrorKind.unauthorized;
  bool get isForbidden => kind == ApiErrorKind.forbidden;

  @override
  String toString() => 'ApiError(${kind.name}, $message)';
}