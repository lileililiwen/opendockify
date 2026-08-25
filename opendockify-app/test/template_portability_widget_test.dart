import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/features/admin/presentation/admin_home_screen.dart';

void main() {
  testWidgets('admin navigation exposes template package controls', (
    tester,
  ) async {
    await tester.pumpWidget(const MaterialApp(home: AdminHomeScreen()));
    expect(find.text('Template packages'), findsOneWidget);
    expect(
      find.text('Validate and import portable template revisions'),
      findsOneWidget,
    );
    expect(find.byIcon(Icons.import_export), findsOneWidget);
  });
}
