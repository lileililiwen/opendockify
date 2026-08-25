import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/template.dart';

void main() {
  group('TemplateDefinitionParser', () {
    test('parses a complete definition', () {
      const json = '''
      {
        "fields": [
          {"name": "amount", "label": "Amount", "type": "currency",
           "required": true,
           "validation": {"min": 0, "max": 1000000, "nonNegative": true}}
        ],
        "clauses": [
          {"id": "cl1", "title": "Force majeure", "text": "Standard clause text."}
        ]
      }
      ''';
      final def = TemplateDefinitionParser.parse(json);
      expect(def.fields, hasLength(1));
      final field = def.fields.first;
      expect(field.name, 'amount');
      expect(field.type, FieldType.currency);
      expect(field.required, isTrue);
      expect(field.validation?.min, 0);
      expect(field.validation?.max, 1000000);
      expect(field.validation?.nonNegative, isTrue);
      expect(def.clauses, hasLength(1));
      expect(def.clauses.first.id, 'cl1');
    });

    test('maps string numeric rules to numbers', () {
      const json =
          '{"fields": [{"name": "n", "label": "N", "type": "number",'
          '"validation": {"min": "1", "maxLength": 4}}]}';
      final field = TemplateDefinitionParser.parse(json).fields.single;
      expect(field.validation?.min, 1);
      expect(field.validation?.maxLength, 4);
    });

    test('defaults unknown field type to text', () {
      const json =
          '{"fields": [{"name": "x", "label": "X", "type": "mystery"}]}';
      final field = TemplateDefinitionParser.parse(json).fields.single;
      expect(field.type, FieldType.text);
    });

    test('throws on empty definition', () {
      expect(
        () => TemplateDefinitionParser.parse(''),
        throwsA(isA<TemplateDefinitionParseException>()),
      );
      expect(
        () => TemplateDefinitionParser.parse('   '),
        throwsA(isA<TemplateDefinitionParseException>()),
      );
    });

    test('throws on malformed JSON', () {
      expect(
        () => TemplateDefinitionParser.parse('{not json'),
        throwsA(isA<TemplateDefinitionParseException>()),
      );
    });

    test('throws when root is not an object', () {
      expect(
        () => TemplateDefinitionParser.parse('[1,2,3]'),
        throwsA(isA<TemplateDefinitionParseException>()),
      );
    });

    test('tolerates missing sections', () {
      final def = TemplateDefinitionParser.parse('{"fields": []}');
      expect(def.fields, isEmpty);
      expect(def.clauses, isEmpty);
    });

    test('recognizes a guided interview definition', () {
      const json = '''
      {
        "fields": [{"name":"name","label":"Name","type":"string","required":true}],
        "clauses": [],
        "interview": {"version":1,"startStepId":"identity","steps":[]}
      }
      ''';

      final interview = TemplateDefinitionParser.parse(json).interview;

      expect(interview, isNotNull);
      expect(interview?.version, 1);
      expect(interview?.startStepId, 'identity');
    });
  });
}
