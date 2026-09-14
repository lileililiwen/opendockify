import 'dart:io';

import 'package:dio/dio.dart';

import '../models/admin.dart';
import '../models/ai.dart';
import '../models/auth.dart';
import '../models/document.dart';
import '../models/integrations.dart';
import '../models/interview.dart';
import '../models/template_dto.dart';
import 'api_error.dart';

/// Holds the current in-memory JWT and refresh handle. The session
/// controller sets/clears them; the [AuthInterceptor] injects the JWT into
/// every request.
class TokenProvider {
  String? token;
  String? refreshToken;
}

class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._tokens);

  final TokenProvider _tokens;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final token = _tokens.token;
    if (token != null && token.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer $token';
    }
    handler.next(options);
  }
}

/// Single typed client for the OpenDockify REST API. Every method normalizes
/// failures into [ApiError]. On a 401 the client first tries one silent
/// refresh via [onRefreshNeeded]; only when that fails does it invoke
/// [onUnauthorized] so the session layer can clear the session.
class ApiClient {
  ApiClient({
    required String baseUrl,
    required TokenProvider tokens,
    this.onUnauthorized,
    this.onRefreshNeeded,
    HttpClientAdapter? httpAdapter,
  })
    // ignore: prefer_initializing_formals
    : _tokens = tokens {
    _dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 10),
        receiveTimeout: const Duration(seconds: 30),
        sendTimeout: const Duration(seconds: 30),
        headers: {'Accept': 'application/json'},
      ),
    );
    if (httpAdapter != null) {
      _dio.httpClientAdapter = httpAdapter;
    }
    _dio.interceptors.add(AuthInterceptor(_tokens));
  }

  final TokenProvider _tokens;
  void Function()? onUnauthorized;

  /// Silent-refresh hook installed by the session layer. Returns true when a
  /// new access token was installed and the failed request may be retried.
  Future<bool> Function()? onRefreshNeeded;
  late final Dio _dio;

  String get baseUrl => _dio.options.baseUrl;

  /// Switches the configured server. Used by the connection screen.
  void setBaseUrl(String baseUrl) {
    _dio.options.baseUrl = baseUrl;
  }

  /// Makes a relative API path (e.g. `/api/documents/{id}/download`) absolute
  /// against the configured server.
  String absoluteUrl(String path) {
    if (path.startsWith('http://') || path.startsWith('https://')) return path;
    final base = baseUrl.replaceFirst(RegExp(r'/+$'), '');
    return '$base/${path.replaceFirst(RegExp(r'^/+'), '')}';
  }

  Future<bool> healthCheck() async {
    try {
      final res = await _dio.get<dynamic>('/healthz');
      final data = res.data;
      return data is Map && data['status'] == 'ok';
    } on DioException {
      return false;
    }
  }

  // ---- Auth ----

  Future<AuthResponse> login(String username, String password) async {
    final data = await _guard(
      () => _dio.post(
        '/api/auth/login',
        data: {'username': username, 'password': password},
      ),
      retryable: false,
    );
    return AuthResponse.fromJson(_asMap(data));
  }

  Future<AuthResponse> register(
    String username,
    String password,
    String? displayName,
  ) async {
    final data = await _guard(
      () => _dio.post(
        '/api/auth/register',
        data: {
          'username': username,
          'password': password,
          'displayName': displayName,
        },
      ),
      retryable: false,
    );
    return AuthResponse.fromJson(_asMap(data));
  }

  Future<UserProfile> me() async {
    final data = await _guard(() => _dio.get('/api/auth/me'));
    return UserProfile.fromJson(_asMap(data));
  }

  /// Rotates the refresh handle. Never retried: a 401 here means the family
  /// was revoked and the session must be cleared.
  Future<AuthResponse> refresh(String refreshToken) async {
    final data = await _guard(
      () => _dio.post('/api/auth/refresh', data: {'refreshToken': refreshToken}),
      retryable: false,
    );
    return AuthResponse.fromJson(_asMap(data));
  }

  /// Revokes the refresh family server-side. Best-effort: failures are
  /// swallowed so local sign-out always succeeds.
  Future<void> logout(String? refreshToken) async {
    if (refreshToken == null || refreshToken.isEmpty) return;
    try {
      await _guard(
        () => _dio.post('/api/auth/logout', data: {'refreshToken': refreshToken}),
        retryable: false,
      );
    } on ApiError {
      // Local sign-out proceeds regardless.
    }
  }

  Future<void> changePassword(String currentPassword, String newPassword) async {
    await _guard(
      () => _dio.post(
        '/api/auth/change-password',
        data: {'currentPassword': currentPassword, 'newPassword': newPassword},
      ),
    );
  }

  /// Starts account recovery. Always succeeds (202) to avoid user enumeration.
  Future<void> recoveryStart(String username) async {
    await _guard(
      () => _dio.post('/api/auth/recovery/start', data: {'username': username}),
      retryable: false,
    );
  }

  Future<void> recoveryComplete(String challengeId, String code, String newPassword) async {
    await _guard(
      () => _dio.post(
        '/api/auth/recovery/complete',
        data: {'challengeId': challengeId, 'code': code, 'newPassword': newPassword},
      ),
      retryable: false,
    );
  }

  Future<AuthResponse> twoFactorVerify(String challengeId, String code) async {
    final data = await _guard(
      () => _dio.post(
        '/api/auth/2fa/verify',
        data: {'challengeId': challengeId, 'code': code},
      ),
      retryable: false,
    );
    return AuthResponse.fromJson(_asMap(data));
  }

  // ---- Templates ----

  Future<List<TemplateSummary>> listMarketplace() async {
    final data = await _guard(() => _dio.get('/api/templates/marketplace'));
    return _asList(data)
        .map((e) => TemplateSummary.fromJson(_asMap(e)))
        .toList();
  }

  Future<Template> getTemplate(String id) async {
    final data = await _guard(() => _dio.get('/api/templates/$id'));
    return Template.fromJson(_asMap(data));
  }

  Future<Template> createTemplate(CreateTemplateRequest request) async {
    final data = await _guard(
      () => _dio.post('/api/templates', data: request.toJson()),
    );
    return Template.fromJson(_asMap(data));
  }

  Future<Template> updateTemplate(
    String id,
    CreateTemplateRequest request,
  ) async {
    final data = await _guard(
      () => _dio.put('/api/templates/$id', data: request.toJson()),
    );
    return Template.fromJson(_asMap(data));
  }

  Future<void> deleteTemplate(String id) async {
    await _guard(() => _dio.delete('/api/templates/$id'));
  }

  Future<Template> copyTemplate(String id) async {
    final data = await _guard(() => _dio.post('/api/templates/$id/copy'));
    return Template.fromJson(_asMap(data));
  }

  Future<List<int>> exportTemplate(String id) async {
    final response = await _guard(
      () => _dio.get<List<int>>(
        '/api/templates/$id/export',
        options: Options(responseType: ResponseType.bytes),
      ),
    );
    return List<int>.from(response as List);
  }

  Future<List<TemplateRevision>> listTemplateRevisions(String id) async {
    final data = await _guard(() => _dio.get('/api/templates/$id/revisions'));
    return _asList(data)
        .map((e) => TemplateRevision.fromJson(_asMap(e)))
        .toList();
  }

  Future<Template> rollbackTemplate(String id, int revision) async {
    final data = await _guard(
      () => _dio.post('/api/templates/$id/revisions/$revision/rollback'),
    );
    return Template.fromJson(_asMap(data));
  }

  Future<PackageValidation> validateTemplatePackage(List<int> bytes) async {
    final data = await _guard(
      () => _dio.post(
        '/api/admin/templates/packages/validate',
        data: bytes,
        options: Options(
          contentType: 'application/vnd.opendockify.template+json',
        ),
      ),
    );
    return PackageValidation.fromJson(_asMap(data));
  }

  Future<Template> importTemplatePackage(
    List<int> bytes,
    String receipt,
    String policy,
  ) async {
    final data = await _guard(
      () => _dio.post(
        '/api/admin/templates/packages/import',
        data: bytes,
        queryParameters: {'receipt': receipt, 'policy': policy},
        options: Options(
          contentType: 'application/vnd.opendockify.template+json',
        ),
      ),
    );
    return Template.fromJson(_asMap(data));
  }

  // ---- Guided interviews ----

  Future<InterviewResponse> createInterview(String templateId) async {
    final data = await _guard(
      () => _dio.post(
        '/api/interviews',
        data: {'templateId': templateId, 'selectedClauseIds': <String>[]},
      ),
    );
    return InterviewResponse.fromJson(_asMap(data));
  }

  Future<InterviewResponse> getInterview(String id) async {
    final data = await _guard(() => _dio.get('/api/interviews/$id'));
    return InterviewResponse.fromJson(_asMap(data));
  }

  Future<InterviewResponse> answerInterview(
    String id,
    int expectedVersion,
    Map<String, String> answers,
  ) async {
    final data = await _guard(
      () => _dio.put(
        '/api/interviews/$id/answer',
        data: {'expectedVersion': expectedVersion, 'answers': answers},
      ),
    );
    return InterviewResponse.fromJson(_asMap(data));
  }

  Future<InterviewResponse> backInterview(
    String id,
    int expectedVersion,
  ) async {
    final data = await _guard(
      () => _dio.post(
        '/api/interviews/$id/back',
        data: {'expectedVersion': expectedVersion},
      ),
    );
    return InterviewResponse.fromJson(_asMap(data));
  }

  Future<InterviewResponse> reviewInterview(String id) async {
    final data = await _guard(() => _dio.get('/api/interviews/$id/review'));
    return InterviewResponse.fromJson(_asMap(data));
  }

  Future<InterviewResponse> completeInterview(String id) async {
    final data = await _guard(() => _dio.post('/api/interviews/$id/complete'));
    return InterviewResponse.fromJson(_asMap(data));
  }

  Future<void> deleteInterview(String id) async {
    await _guard(() => _dio.delete('/api/interviews/$id'));
  }

  // ---- Documents ----

  Future<DocumentListPage> listDocuments({
    int page = 1,
    int pageSize = 20,
    String search = '',
    String archive = 'active',
    String sort = 'newest',
  }) async {
    final data = await _guard(
      () => _dio.get(
        '/api/documents',
        queryParameters: {
          'page': page,
          'pageSize': pageSize,
          if (search.trim().isNotEmpty) 'search': search.trim(),
          'archive': archive,
          'sort': sort,
        },
      ),
    );
    return DocumentListPage.fromJson(_asMap(data));
  }

  Future<GenerateResult> generateDocument(
    GenerateDocumentRequest request,
  ) async {
    final data = await _guard(
      () => _dio.post('/api/documents/generate', data: request.toJson()),
    );
    return GenerateResult.fromJson(_asMap(data));
  }

  Future<PreviewResult> previewDocument(GenerateDocumentRequest request) async {
    final data = await _guard(
      () => _dio.post('/api/documents/preview', data: request.toJson()),
    );
    return PreviewResult.fromJson(_asMap(data));
  }

  Future<GenerateResult> finalizeDocument(
    GenerateDocumentRequest request,
  ) async {
    final data = await _guard(
      () => _dio.post('/api/documents/finalize', data: request.toJson()),
    );
    return GenerateResult.fromJson(_asMap(data));
  }

  Future<DocumentView> getDocument(String id) async {
    final data = await _guard(() => _dio.get('/api/documents/$id'));
    return DocumentView.fromJson(_asMap(data));
  }

  Future<GenerateResult> reeditDocument(
    String id,
    GenerateDocumentRequest request,
  ) async {
    final data = await _guard(
      () => _dio.post('/api/documents/$id/reedit', data: request.toJson()),
    );
    return GenerateResult.fromJson(_asMap(data));
  }

  Future<void> deleteDocument(String id) async {
    await _guard(() => _dio.delete('/api/documents/$id'));
  }

  Future<DocumentView> updateDocumentMetadata(
    String id, {
    required String title,
    required bool isArchived,
  }) async {
    final data = await _guard(
      () => _dio.put(
        '/api/documents/$id/metadata',
        data: {'title': title, 'isArchived': isArchived},
      ),
    );
    return DocumentView.fromJson(_asMap(data));
  }

  Future<List<DocumentSummary>> getDocumentVersions(String id) async {
    final data = await _guard(() => _dio.get('/api/documents/$id/versions'));
    return _asList(data)
        .map((item) => DocumentSummary.fromJson(_asMap(item)))
        .toList();
  }

  /// Downloads the PDF for a document into the given [filePath]. Returns the
  /// local path on success.
  Future<String> downloadPdf(String downloadUrl, String filePath) async {
    try {
      final res = await _dio.get<List<int>>(
        absoluteUrl(downloadUrl),
        options: Options(responseType: ResponseType.bytes),
      );
      final bytes = res.data;
      if (bytes == null) {
        throw const ApiError(ApiErrorKind.unknown, 'Empty download response.');
      }
      final file = File(filePath);
      await file.writeAsBytes(bytes, flush: true);
      return file.path;
    } on DioException catch (e) {
      throw _mapError(e);
    }
  }

  Future<List<DocumentShareItem>> listDocumentShares(String id) async {
    final data = await _guard(() => _dio.get('/api/documents/$id/shares'));
    return _asList(data)
        .map((e) => DocumentShareItem.fromJson(_asMap(e)))
        .toList();
  }

  Future<void> createDocumentGrant(
    String id,
    String username,
    String accessLevel,
  ) async {
    await _guard(
      () => _dio.post(
        '/api/documents/$id/shares/grants',
        data: {'username': username, 'accessLevel': accessLevel},
      ),
    );
  }

  Future<CreatedDocumentShareLink> createDocumentShareLink(
    String id,
    int lifetimeHours,
    bool allowDownload,
  ) async {
    final data = await _guard(
      () => _dio.post(
        '/api/documents/$id/shares/links',
        data: {'lifetimeHours': lifetimeHours, 'allowDownload': allowDownload},
      ),
    );
    return CreatedDocumentShareLink.fromJson(_asMap(data));
  }

  Future<void> revokeDocumentShare(
    String documentId,
    DocumentShareItem share,
  ) async {
    final segment = share.kind == 'grant' ? 'grants' : 'links';
    await _guard(
      () =>
          _dio.delete('/api/documents/$documentId/shares/$segment/${share.id}'),
    );
  }

  Future<List<ShareAuditEntry>> getDocumentShareAudit(String id) async {
    final data = await _guard(
      () => _dio.get(
        '/api/documents/$id/shares/audit',
        queryParameters: {'page': 1, 'pageSize': 50},
      ),
    );
    final map = _asMap(data);
    return _asList(map['items'])
        .map((e) => ShareAuditEntry.fromJson(_asMap(e)))
        .toList();
  }

  // ---- AI ----

  Future<PolishResult> polishClause(PolishClauseRequest request) async {
    final data = await _guard(
      () => _dio.post('/api/ai/polish-clause', data: request.toJson()),
    );
    return PolishResult.fromJson(_asMap(data));
  }

  Future<PolishResult> polishDocument(PolishDocumentRequest request) async {
    final data = await _guard(
      () => _dio.post('/api/ai/polish-document', data: request.toJson()),
    );
    return PolishResult.fromJson(_asMap(data));
  }

  // ---- Admin ----

  Future<List<SettingView>> getSettings() async {
    final data = await _guard(() => _dio.get('/api/admin/settings'));
    return _asList(data).map((e) => SettingView.fromJson(_asMap(e))).toList();
  }

  Future<void> updateSetting(String key, String value) async {
    await _guard(
      () => _dio.put('/api/admin/settings/$key', data: {'value': value}),
    );
  }

  Future<Template> adminUpsertTemplate({
    String? id,
    required CreateTemplateRequest request,
  }) async {
    final data = await _guard(
      () => _dio.post(
        '/api/admin/templates',
        data: {'id': ?id, ...request.toJson()},
      ),
    );
    return Template.fromJson(_asMap(data));
  }

  Future<List<AiUsageEntry>> listAiUsage({int limit = 50}) async {
    final data = await _guard(
      () => _dio.get('/api/admin/ai-usage', queryParameters: {'limit': limit}),
    );
    return _asList(data).map((e) => AiUsageEntry.fromJson(_asMap(e))).toList();
  }


  // ---- Integrations management ----

  Future<List<ServiceTokenView>> listTokens() async {
    final data = await _guard(() => _dio.get<dynamic>('/api/integrations/tokens'));
    return _asList(data).map((e) => ServiceTokenView.fromJson(_asMap(e))).toList();
  }

  Future<ServiceTokenIssuance> createToken({
    required String name,
    required List<String> scopes,
    required int expiresInDays,
  }) async {
    final data = await _guard(() => _dio.post<dynamic>(
          '/api/integrations/tokens',
          data: {'name': name, 'scopes': scopes, 'expiresInDays': expiresInDays},
        ));
    return ServiceTokenIssuance.fromJson(_asMap(data));
  }

  Future<void> revokeToken(String id) async {
    await _guard(() => _dio.delete<void>('/api/integrations/tokens/$id'));
  }

  Future<List<WebhookSubscriptionView>> listSubscriptions() async {
    final data =
        await _guard(() => _dio.get<dynamic>('/api/integrations/webhooks/subscriptions'));
    return _asList(data).map((e) => WebhookSubscriptionView.fromJson(_asMap(e))).toList();
  }

  Future<SubscriptionCreated> createSubscription({
    required String url,
    required List<String> eventTypes,
  }) async {
    final data = await _guard(() => _dio.post<dynamic>(
          '/api/integrations/webhooks/subscriptions',
          data: {'url': url, 'eventTypes': eventTypes},
        ));
    return SubscriptionCreated.fromJson(_asMap(data));
  }

  Future<String> rotateSubscriptionSecret(String id) async {
    final data = await _guard(
      () => _dio.post<dynamic>('/api/integrations/webhooks/subscriptions/$id/rotate-secret'),
    );
    return _asMap(data)['secret']?.toString() ?? '';
  }

  Future<void> deleteSubscription(String id) async {
    await _guard(
      () => _dio.delete<void>('/api/integrations/webhooks/subscriptions/$id'),
    );
  }

  Future<List<WebhookDeliveryView>> listDeliveries({String? subscriptionId, int limit = 50}) async {
    final data = await _guard(() => _dio.get<dynamic>(
          '/api/integrations/webhooks/deliveries',
          queryParameters: {
            'subscriptionId': ?subscriptionId,
            'limit': limit,
          },
        ));
    return _asList(data).map((e) => WebhookDeliveryView.fromJson(_asMap(e))).toList();
  }

  Future<void> retryDelivery(String id) async {
    await _guard(
      () => _dio.post<dynamic>('/api/integrations/webhooks/deliveries/$id/retry'),
    );
  }

  // ---- Helpers ----

  Future<dynamic> _guard(
    Future<Response<dynamic>> Function() run, {
    bool retryable = true,
  }) async {
    try {
      final res = await run();
      return res.data;
    } on DioException catch (e) {
      if (e.response?.statusCode == 401 && retryable && onRefreshNeeded != null) {
        final refreshed = await onRefreshNeeded!();
        if (refreshed) {
          try {
            final res = await run();
            return res.data;
          } on DioException catch (retryError) {
            throw _mapError(retryError);
          }
        }
      }
      throw _mapError(e);
    }
  }

  ApiError _mapError(DioException e) {
    final status = e.response?.statusCode;
    final message = _extractError(e.response?.data);
    if (e.type == DioExceptionType.connectionError ||
        e.type == DioExceptionType.connectionTimeout ||
        e.type == DioExceptionType.receiveTimeout ||
        e.type == DioExceptionType.sendTimeout) {
      return const ApiError(
        ApiErrorKind.network,
        'Cannot reach server. Check the connection settings.',
      );
    }
    if (status == null) {
      return ApiError(
        ApiErrorKind.unknown,
        message ?? 'Something went wrong.',
        statusCode: status,
      );
    }
    switch (status) {
      case 401:
        onUnauthorized?.call();
        return ApiError(
          ApiErrorKind.unauthorized,
          message ?? 'Session expired. Please log in again.',
          statusCode: status,
        );
      case 403:
        final kind = (message?.toLowerCase().contains('ai disabled') ?? false)
            ? ApiErrorKind.aiDisabled
            : ApiErrorKind.forbidden;
        return ApiError(kind, message ?? 'Forbidden.', statusCode: status);
      case 404:
        return ApiError(
          ApiErrorKind.notFound,
          message ?? 'Not found.',
          statusCode: status,
        );
      case 400:
      case 409:
      case 413:
        return ApiError(
          ApiErrorKind.validation,
          message ?? 'Invalid request.',
          statusCode: status,
        );
      case 429:
        return ApiError(
          ApiErrorKind.rateLimited,
          message ?? 'Rate limit reached.',
          statusCode: status,
        );
      case >= 500:
        return ApiError(
          ApiErrorKind.server,
          message ?? 'The server reported an error.',
          statusCode: status,
        );
      default:
        return ApiError(
          ApiErrorKind.unknown,
          message ?? 'Something went wrong.',
          statusCode: status,
        );
    }
  }

  static String? _extractError(Object? data) {
    if (data is Map) {
      final value = data['error'];
      if (value is String && value.isNotEmpty) return value;
    }
    if (data is String && data.isNotEmpty) return data;
    return null;
  }

  static Map<String, dynamic> _asMap(Object? value) {
    if (value is Map<String, dynamic>) return value;
    if (value is Map) return value.map((k, v) => MapEntry(k.toString(), v));
    return <String, dynamic>{};
  }

  static List<dynamic> _asList(Object? value) =>
      value is List ? value : const [];
}
