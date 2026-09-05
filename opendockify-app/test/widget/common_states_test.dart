import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/api/api_error.dart';
import 'package:opendockify_app/core/errors/error_presenter.dart';
import 'package:opendockify_app/core/widgets/common.dart';

void main() {
  test('safe error copy never exposes exception details', () {
    expect(
      safeErrorMessage(StateError('secret stack detail')),
      'Something went wrong. Please try again.',
    );
    expect(
      safeErrorMessage(const ApiError(ApiErrorKind.network, 'socket detail')),
      'Cannot reach server. Check the connection settings.',
    );
  });

  testWidgets('empty state exposes its meaning and action', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: EmptyState(
          title: 'No documents',
          message: 'Create a document to get started.',
          action: FilledButton(
            onPressed: () {},
            child: const Text('Create document'),
          ),
        ),
      ),
    );

    expect(find.text('No documents'), findsOneWidget);
    expect(find.text('Create document'), findsOneWidget);
    expect(
      tester.getSemantics(find.byType(EmptyState)).label,
      contains('No documents. Create a document to get started.'),
    );
  });
}
