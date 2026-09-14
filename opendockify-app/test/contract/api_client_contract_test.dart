import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/api/api_client.dart';
import 'package:opendockify_app/core/api/api_error.dart';
import 'package:opendockify_app/core/models/document.dart';

/// Minimal in-memory Dio adapter that dispatches canned responses per endpoint
/// and records the requests so the test can assert the exact HTTP contract
/// (method, path, body) the client sends to the backend.
class FakeAdapter implements HttpClientAdapter {
  final List<RequestOptions> requests = [];
  Object? Function(RequestOptions options)? responder;

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    requests.add(options);
    final body = responder?.call(options);
    if (body is ResponseBody) {
      return body;
    }
    final statusCode = body is int ? body : 200;
    final data = body is int ? null : body;
    return ResponseBody.fromString(
      data == null ? '' : jsonEncode(data),
      statusCode,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  late ApiClient client;
  late FakeAdapter adapter;
  late TokenProvider tokens;
  final unauthCalls = <int>[];

  setUp(() {
    tokens = TokenProvider()..token = 'jwt';
    adapter = FakeAdapter();
    client = ApiClient(
      baseUrl: 'http://test',
      tokens: tokens,
      httpAdapter: adapter,
    );
    client.onUnauthorized = () => unauthCalls.add(1);
    adapterResponds(adapter);
  });

  // The client's Dio is private; for the contract test we drive the real
  // ApiClient via a small shim that owns its own Dio using the same adapter.
  test('login posts username/password and parses AuthResponse', () async {
    final auth = await client.login('alice', 'secret');
    expect(auth.token, 'jwt2');
    expect(auth.username, 'alice');
    expect(adapter.requests.last.method, 'POST');
    expect(adapter.requests.last.path, '/api/auth/login');
    final body = adapter.requests.last.data as Map<String, dynamic>;
    expect(body['username'], 'alice');
    expect(body['password'], 'secret');
    expect(adapter.requests.last.headers['Authorization'], 'Bearer jwt');
  });

  test('marketplace lists templates', () async {
    final list = await client.listMarketplace();
    expect(list, hasLength(1));
    expect(list.first.id, 't1');
  });

  test('getTemplate parses full template', () async {
    final template = await client.getTemplate('t1');
    expect(template.name, 'Loan');
    expect(template.definitionJson, '{"fields":[]}');
  });

  test('listDocuments sends page and library query parameters', () async {
    final page = await client.listDocuments(
      page: 2,
      pageSize: 10,
      search: 'loan',
      archive: 'archived',
      sort: 'title',
    );
    expect(page.page, 1);
    expect(adapter.requests.last.queryParameters['page'], 2);
    expect(adapter.requests.last.queryParameters['pageSize'], 10);
    expect(adapter.requests.last.queryParameters['search'], 'loan');
    expect(adapter.requests.last.queryParameters['archive'], 'archived');
    expect(adapter.requests.last.queryParameters['sort'], 'title');
  });

  test('updateDocumentMetadata sends title and archive state', () async {
    final document = await client.updateDocumentMetadata(
      'd1',
      title: 'Renamed',
      isArchived: true,
    );
    expect(document.title, 'Renamed');
    expect(adapter.requests.last.method, 'PUT');
    expect(adapter.requests.last.path, '/api/documents/d1/metadata');
    expect(adapter.requests.last.data, {
      'title': 'Renamed',
      'isArchived': true,
    });
  });

  test('getDocumentVersions parses version summaries', () async {
    final versions = await client.getDocumentVersions('d1');
    expect(versions.single.title, 'Renamed');
    expect(adapter.requests.last.path, '/api/documents/d1/versions');
  });

  test('generateDocument posts the snapshot contract', () async {
    final result = await client.generateDocument(
      const GenerateDocumentRequest(
        templateId: 't1',
        values: {'amount': '1000'},
        selectedClauseIds: ['cl1'],
      ),
    );
    expect(result.document.id, 'd1');
    final body = adapter.requests.last.data as Map<String, dynamic>;
    expect(body['templateId'], 't1');
    expect((body['values'] as Map)['amount'], '1000');
    expect(body['selectedClauseIds'], ['cl1']);
  });

  test('previewDocument posts snapshot without finalizing', () async {
    final preview = await client.previewDocument(
      const GenerateDocumentRequest(
        templateId: 't1',
        values: {'amount': '1000'},
      ),
    );
    expect(preview.renderedText, 'Preview');
    expect(adapter.requests.last.path, '/api/documents/preview');
  });

  test('finalizeDocument uses explicit finalization route', () async {
    final result = await client.finalizeDocument(
      const GenerateDocumentRequest(
        templateId: 't1',
        values: {'amount': '1000'},
      ),
    );
    expect(result.document.id, 'd1');
    expect(adapter.requests.last.path, '/api/documents/finalize');
  });

  test(
    'guided interview create and answer use versioned session contract',
    () async {
      final created = await client.createInterview('t1');
      expect(created.session?.currentStep?.id, 'identity');
      expect(adapter.requests.last.path, '/api/interviews');

      final answered = await client.answerInterview('s1', 0, const {
        'name': 'Alice',
      });
      expect(answered.session?.version, 1);
      expect(adapter.requests.last.path, '/api/interviews/s1/answer');
      expect(adapter.requests.last.data, {
        'expectedVersion': 0,
        'answers': {'name': 'Alice'},
      });
    },
  );

  test('settings returns SettingView list', () async {
    final settings = await client.getSettings();
    expect(settings.single.key, 'Ai:Enabled');
  });

  test('401 triggers onUnauthorized and maps to unauthorized', () async {
    adapter.responder = (options) => 401;
    await expectLater(
      client.me(),
      throwsA(
        isA<ApiError>().having(
          (e) => e.kind,
          'kind',
          ApiErrorKind.unauthorized,
        ),
      ),
    );
    expect(unauthCalls, hasLength(1));
  });

  test('500 maps to server error', () async {
    adapter.responder = (options) => 500;
    await expectLater(
      client.me(),
      throwsA(
        isA<ApiError>().having((e) => e.kind, 'kind', ApiErrorKind.server),
      ),
    );
  });

  test('404 maps to not found', () async {
    adapter.responder = (options) => 404;
    await expectLater(
      client.me(),
      throwsA(
        isA<ApiError>().having((e) => e.kind, 'kind', ApiErrorKind.notFound),
      ),
    );
  });

  test('every request sends a generated X-Correlation-Id', () async {
    await client.me();
    final headers = adapter.requests.last.headers;
    final id = headers['X-Correlation-Id'] as String?;
    expect(id, isNotNull);
    expect(id, matches(RegExp(r'^[0-9a-f-]{36}$')));
  });

  test('correlation ids are unique per request', () async {
    await client.me();
    await client.listMarketplace();
    final first = adapter.requests[0].headers['X-Correlation-Id'];
    final second = adapter.requests[1].headers['X-Correlation-Id'];
    expect(first, isNot(second));
  });

  test('error body with code surfaces on ApiError', () async {
    adapter.responder = (options) => ResponseBody.fromString(
      jsonEncode({
        'error': {'code': 'validation_failed', 'message': 'amount must be > 0'},
      }),
      400,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
    await expectLater(
      client.me(),
      throwsA(
        isA<ApiError>()
            .having((e) => e.kind, 'kind', ApiErrorKind.validation)
            .having((e) => e.code, 'code', 'validation_failed')
            .having((e) => e.message, 'message', 'amount must be > 0'),
      ),
    );
  });

  test('echoed X-Correlation-Id is captured on ApiError', () async {
    final correlationId = 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee';
    adapter.responder = (options) => ResponseBody.fromString(
      jsonEncode({'error': {'code': 'server', 'message': 'boom'}}),
      500,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
        'x-correlation-id': [correlationId],
      },
    );
    try {
      await client.me();
      fail('expected ApiError');
    } on ApiError catch (e) {
      expect(e.correlationId, correlationId);
      expect(e.code, 'server');
    }
  });
}

void adapterResponds(FakeAdapter a) {
  a.responder = (options) {
    switch (options.path) {
      case '/healthz':
        return {'status': 'ok'};
      case '/api/auth/login':
        return {
          'id': 'u1',
          'username': 'alice',
          'role': 'User',
          'token': 'jwt2',
        };
      case '/api/auth/me':
        return {
          'id': 'u1',
          'username': 'alice',
          'displayName': 'Alice',
          'role': 'User',
          'isAdministrator': false,
        };
      case '/api/templates/marketplace':
        return [
          {
            'id': 't1',
            'name': 'Loan',
            'category': 'finance',
            'description': 'd',
            'isBuiltIn': true,
            'isPublic': true,
          },
        ];
      case '/api/documents':
        return {
          'items': [],
          'page': 1,
          'pageSize': 20,
          'totalCount': 0,
          'totalPages': 0,
        };
      case '/api/documents/generate':
      case '/api/documents/finalize':
        return {
          'document': {
            'id': 'd1',
            'templateId': 't1',
            'status': 'Generated',
            'signingStatus': 'NotStarted',
            'snapshotJson': '{"values":{}}',
            'renderedText': 'Rendered',
          },
          'warnings': [],
        };
      case '/api/documents/preview':
        return {
          'templateName': 'Loan IOU',
          'renderedText': 'Preview',
          'warnings': [],
        };
      case '/api/interviews':
        return _interviewResponse(0);
      case '/api/interviews/s1/answer':
        return _interviewResponse(1);
      case '/api/documents/d1/metadata':
        return {
          'id': 'd1',
          'title': 'Renamed',
          'templateId': 't1',
          'templateName': 'Loan IOU',
          'isArchived': true,
          'status': 'Generated',
          'signingStatus': 'NotStarted',
          'snapshotJson': '{"values":{}}',
          'renderedText': 'Rendered',
        };
      case '/api/documents/d1/versions':
        return [
          {
            'id': 'd1',
            'title': 'Renamed',
            'templateId': 't1',
            'templateName': 'Loan IOU',
            'isArchived': true,
            'status': 'Generated',
            'signingStatus': 'NotStarted',
            'createdAt': '2026-01-01T00:00:00Z',
          },
        ];
      case '/api/admin/settings':
        return [
          {
            'key': 'Ai:Enabled',
            'value': 'true',
            'source': 'appsettings',
            'isSecret': false,
            'masked': false,
          },
        ];
      default:
        if (options.path == '/api/templates/t1') {
          return {
            'id': 't1',
            'name': 'Loan',
            'category': 'finance',
            'description': 'd',
            'riskNoticeText': 'n',
            'body': 'Body {{amount}}',
            'definitionJson': '{"fields":[]}',
            'isBuiltIn': true,
            'isPublic': true,
          };
        }
        return 404;
    }
  };
}

Map<String, dynamic> _interviewResponse(int version) => {
  'session': {
    'id': 's1',
    'templateId': 't1',
    'version': version,
    'expiresAt': '2026-09-01T00:00:00Z',
    'answers': version == 0 ? <String, String>{} : {'name': 'Alice'},
    'selectedClauseIds': <String>[],
    'readyForReview': false,
    'currentStep': {
      'id': 'identity',
      'title': 'Identity',
      'position': 1,
      'total': 1,
      'fields': [
        {'name': 'name', 'label': 'Name', 'type': 'string', 'required': true},
      ],
    },
  },
  'review': <Object>[],
  'preview': null,
};
