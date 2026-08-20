import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/template.dart';
import 'package:opendockify_app/features/templates/presentation/dynamic_template_form.dart';

const _definitionJson = '''
{
  "fields": [
    {"name": "amount", "label": "Amount", "type": "currency", "required": true,
     "validation": {"nonNegative": true}},
    {"name": "rate", "label": "Rate", "type": "number",
     "validation": {"min": 0, "max": 20}},
    {"name": "title", "label": "Title", "type": "text", "required": true,
     "validation": {"minLength": 2}},
    {"name": "start", "label": "Start date", "type": "date"}
  ],
  "clauses": [
    {"id": "cl1", "title": "Force majeure", "text": "Standard text."},
    {"id": "cl2", "title": "Governing law", "text": "Law text."}
  ]
}
''';

Widget _wrap(Widget child) {
  return MaterialApp(home: Scaffold(body: child));
}

/// Sets the text of a form field by label. Works for the read-only date field
/// too because it writes to the field's own controller.
Future<void> _setField(WidgetTester tester, String label, String text) async {
  final field = tester.widget<TextFormField>(find.widgetWithText(TextFormField, label));
  field.controller!.text = text;
  await tester.pump();
}

void main() {
  final definition = TemplateDefinitionParser.parse(_definitionJson);

  testWidgets('rejects empty required fields', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(definition: definition)));
    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    expect(state.submit(), isNull);
    await tester.pump();
    expect(find.text('Amount is required.'), findsOneWidget);
    expect(find.text('Title is required.'), findsOneWidget);
  });

  testWidgets('collects values and selected clauses on valid submit', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(definition: definition)));

    await _setField(tester, 'Amount', '1000.50');
    await _setField(tester, 'Rate', '5');
    await _setField(tester, 'Title', 'My Loan');
    await _setField(tester, 'Start date', '2026-01-15');

    await tester.tap(find.text('Force majeure'));
    await tester.pump();

    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    final result = state.submit();
    expect(result, isNotNull);
    expect(result!.values['amount'], '1000.50');
    expect(result.values['rate'], '5');
    expect(result.values['title'], 'My Loan');
    expect(result.values['start'], '2026-01-15');
    expect(result.selectedClauseIds, ['cl1']);
  });

  testWidgets('rejects non-numeric amount', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(definition: definition)));
    await _setField(tester, 'Amount', 'abc');
    await _setField(tester, 'Title', 'X');
    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    expect(state.submit(), isNull);
    await tester.pump();
    expect(find.text('Amount must be a number.'), findsOneWidget);
  });

  testWidgets('rejects negative currency when nonNegative', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(definition: definition)));
    await _setField(tester, 'Amount', '-5');
    await _setField(tester, 'Title', 'X');
    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    expect(state.submit(), isNull);
    await tester.pump();
    expect(find.text('Amount must not be negative.'), findsOneWidget);
  });

  testWidgets('rejects rate above max', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(definition: definition)));
    await _setField(tester, 'Amount', '100');
    await _setField(tester, 'Rate', '99');
    await _setField(tester, 'Title', 'OK');
    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    expect(state.submit(), isNull);
    await tester.pump();
    expect(find.text('Rate must be at most 20.'), findsOneWidget);
  });

  testWidgets('rejects invalid date', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(definition: definition)));
    await _setField(tester, 'Amount', '100');
    await _setField(tester, 'Title', 'OK');
    await _setField(tester, 'Start date', 'not-a-date');
    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    expect(state.submit(), isNull);
    await tester.pump();
    expect(find.text('Start date must be a valid date.'), findsOneWidget);
  });

  testWidgets('prefills initial values and clause selection', (tester) async {
    await tester.pumpWidget(_wrap(DynamicTemplateForm(
      definition: definition,
      initialValues: const {'amount': '500', 'title': 'Prefilled'},
      initialClauseIds: const {'cl2'},
    )));
    final state = tester.state<DynamicTemplateFormState>(find.byType(DynamicTemplateForm));
    final result = state.submit();
    expect(result!.values['amount'], '500');
    expect(result.values['title'], 'Prefilled');
    expect(result.selectedClauseIds, ['cl2']);
  });
}