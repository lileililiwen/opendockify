import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/api/api_client.dart';
import 'package:opendockify_app/core/providers.dart';
import 'package:opendockify_app/features/documents/presentation/document_detail_screen.dart';

class _SharingAdapter implements HttpClientAdapter {
  _SharingAdapter(this.isOwner);
  final bool isOwner;
  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final body = options.path.endsWith('/versions')
        ? <Object>[]
        : {
            'id': 'd1',
            'title': 'Shared contract',
            'templateId': 't1',
            'templateName': 'Contract',
            'isArchived': false,
            'status': 'Generated',
            'signingStatus': 'NotInitiated',
            'snapshotJson': '{}',
            'renderedText': 'Immutable text',
            'createdAt': '2026-08-25T00:00:00Z',
            'downloadUrl': '/api/documents/d1/download',
            'isOwner': isOwner,
            'accessLevel': isOwner ? 'owner' : 'view',
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

void main() {
  Future<void> pump(WidgetTester tester, bool owner) async {
    final client = ApiClient(
      baseUrl: 'http://test',
      tokens: TokenProvider()..token = 'jwt',
      httpAdapter: _SharingAdapter(owner),
    );
    await tester.pumpWidget(
      ProviderScope(
        overrides: [apiClientProvider.overrideWithValue(client)],
        child: const MaterialApp(home: DocumentDetailScreen(documentId: 'd1')),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('shared viewer has read controls without owner mutations', (
    tester,
  ) async {
    await pump(tester, false);
    expect(find.text('Shared view access'), findsOneWidget);
    expect(find.text('Download PDF'), findsOneWidget);
    expect(find.text('Sharing and access history'), findsNothing);
    expect(find.text('Rename'), findsNothing);
    expect(find.text('Delete'), findsNothing);
  });

  testWidgets('owner sees sharing management entry point', (tester) async {
    await pump(tester, true);
    expect(find.text('Sharing and access history'), findsOneWidget);
    expect(find.text('Rename'), findsOneWidget);
  });
}
