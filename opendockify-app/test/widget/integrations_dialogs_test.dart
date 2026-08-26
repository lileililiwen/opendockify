import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/integrations.dart';
import 'package:opendockify_app/features/integrations/presentation/integration_dialogs.dart';

Widget _host(WidgetBuilder builder) =>
    MaterialApp(home: Scaffold(body: Builder(builder: builder)));

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('integrations DTOs parse server payloads', () {
    final token = ServiceTokenView.fromJson({
      'id': 't1',
      'name': 'ci',
      'prefix': 'abc123',
      'scopes': 'templates:read,documents:write',
      'createdAtUtc': '2026-08-26T00:00:00Z',
      'expiresAtUtc': '2026-11-26T00:00:00Z',
      'revokedAtUtc': null,
      'lastUsedAtUtc': '2026-08-26T01:00:00Z',
      'useCount': 3,
    });
    expect(token.scopes, ['templates:read', 'documents:write']);
    expect(token.isRevoked, isFalse);
    expect(token.useCount, 3);

    final delivery = WebhookDeliveryView.fromJson({
      'id': 'd1',
      'subscriptionId': 's1',
      'eventId': 'e1',
      'state': 'Exhausted',
      'attemptCount': 2,
      'lastStatusCode': 500,
      'lastError': 'server error',
      'blockedReason': null,
      'attempts': [
        {'atUtc': '2026-08-26T00:01:00Z', 'statusCode': 500, 'error': null},
        {'atUtc': '2026-08-26T00:02:00Z', 'statusCode': null, 'error': 'timeout'},
      ],
    });
    expect(delivery.attempts.length, 2);
    expect(delivery.attempts[1].error, 'timeout');
    expect(delivery.isRetryable, isTrue);
  });

  testWidgets('secret dialog shows the value once with a copy affordance',
      (tester) async {
    await tester.pumpWidget(_host((context) {
      return TextButton(
        onPressed: () => showSecretOnce(context, 'Service token created', 'odk_abc_def123'),
        child: const Text('reveal'),
      );
    }));

    await tester.tap(find.text('reveal'));
    await tester.pumpAndSettle();

    expect(find.text('odk_abc_def123'), findsOneWidget);
    expect(find.text('Copy'), findsOneWidget);
    expect(find.text('Done'), findsOneWidget);

    await tester.tap(find.text('Copy'));
    await tester.pump();
    expect(find.text('Copied to clipboard.'), findsOneWidget);

    // Dismissing is the only way out; afterwards nothing re-shows it.
    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();
    expect(find.text('odk_abc_def123'), findsNothing);
  });

  testWidgets('confirm action returns true only after explicit confirmation',
      (tester) async {
    bool? result;

    await tester.pumpWidget(_host((context) {
      return TextButton(
        onPressed: () async {
          result = await confirmAction(context, 'Revoke token?');
        },
        child: const Text('ask'),
      );
    }));

    await tester.tap(find.text('ask'));
    await tester.pumpAndSettle();
    expect(find.text('Revoke token?'), findsOneWidget);

    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
    expect(result, isFalse);

    await tester.tap(find.text('ask'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm'));
    await tester.pumpAndSettle();
    expect(result, isTrue);
  });
}
