import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/api/api_client.dart';
import 'package:opendockify_app/core/models/document.dart';
import 'package:opendockify_app/core/models/template_dto.dart';
import 'package:opendockify_app/core/providers.dart';
import 'package:opendockify_app/core/storage/draft_store.dart';
import 'package:opendockify_app/features/documents/presentation/guided_interview_form.dart';

class _Adapter implements HttpClientAdapter {
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final body = {
      'session': {
        'id': 'session-1',
        'templateId': 'template-1',
        'version': 0,
        'expiresAt': '2026-09-01T00:00:00Z',
        'answers': <String, String>{},
        'selectedClauseIds': <String>[],
        'readyForReview': false,
        'currentStep': {
          'id': 'identity',
          'title': 'Identity',
          'position': 1,
          'total': 2,
          'fields': [
            {
              'name': 'name',
              'label': 'Name',
              'type': 'string',
              'required': true,
            },
          ],
        },
      },
      'review': <Object>[],
      'preview': null,
    };
    return ResponseBody.fromString(
      jsonEncode(body),
      200,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType],
      },
    );
  }

  @override
  void close({bool force = false}) {}
}

class _DraftStore implements DraftStore {
  LocalDocumentDraft? value;

  @override
  Future<void> clear(String key) async => value = null;

  @override
  Future<LocalDocumentDraft?> read(String key) async => value;

  @override
  Future<void> write(String key, LocalDocumentDraft draft) async =>
      value = draft;
}

void main() {
  testWidgets(
    'shows server-authoritative step progress and persists resume id',
    (tester) async {
      final draftStore = _DraftStore();
      final client = ApiClient(
        baseUrl: 'http://test',
        tokens: TokenProvider()..token = 'jwt',
        httpAdapter: _Adapter(),
      );
      const template = Template(
        id: 'template-1',
        name: 'Guided template',
        category: 'Test',
        description: '',
        riskNoticeText: '',
        body: '{{name}}',
        definitionJson: '{}',
        isBuiltIn: false,
        isPublic: false,
      );

      await tester.pumpWidget(
        ProviderScope(
          overrides: [
            apiClientProvider.overrideWithValue(client),
            draftStoreProvider.overrideWithValue(draftStore),
          ],
          child: MaterialApp(
            home: Scaffold(
              body: GuidedInterviewForm(
                template: template,
                draftKey: 'draft-key',
                onGenerated: (_) {},
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Step 1 of 2'), findsOneWidget);
      expect(find.text('Identity'), findsOneWidget);
      expect(find.text('Save and continue'), findsOneWidget);
      expect(draftStore.value?.interviewSessionId, 'session-1');
    },
  );
}
