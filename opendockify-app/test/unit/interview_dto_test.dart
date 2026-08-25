import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/interview.dart';

void main() {
  test('parses resumable session, step progress, review, and preview', () {
    final response = InterviewResponse.fromJson({
      'session': {
        'id': 'session-1',
        'templateId': 'template-1',
        'version': 2,
        'expiresAt': '2026-09-01T00:00:00Z',
        'answers': {'name': 'Alice'},
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
    });

    expect(response.session?.id, 'session-1');
    expect(response.session?.version, 2);
    expect(response.session?.answers['name'], 'Alice');
    expect(response.session?.currentStep?.position, 1);
    expect(response.session?.currentStep?.fields.single.label, 'Name');
  });
}
