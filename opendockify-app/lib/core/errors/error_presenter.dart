import '../api/api_error.dart';

/// Maps an [ApiError] to a friendly, non-technical user message.
String friendlyErrorMessage(ApiError error) {
  switch (error.kind) {
    case ApiErrorKind.network:
      return 'Cannot reach server. Check the connection settings.';
    case ApiErrorKind.unauthorized:
      return error.message;
    case ApiErrorKind.forbidden:
      return error.message.isEmpty ? 'You are not allowed to do that.' : error.message;
    case ApiErrorKind.notFound:
      return error.message.isEmpty ? 'Not found.' : error.message;
    case ApiErrorKind.validation:
      return error.message.isEmpty ? 'Please check your input.' : error.message;
    case ApiErrorKind.rateLimited:
      return error.message.isEmpty ? 'Rate limit reached. Try again later.' : error.message;
    case ApiErrorKind.aiDisabled:
      return 'AI is disabled by the administrator.';
    case ApiErrorKind.server:
      return 'The server reported an error. Please try again.';
    case ApiErrorKind.unknown:
      return 'Something went wrong. Please try again.';
  }
}