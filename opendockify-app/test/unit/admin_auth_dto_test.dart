import 'package:flutter_test/flutter_test.dart';
import 'package:opendockify_app/core/models/admin.dart';
import 'package:opendockify_app/core/models/ai.dart';
import 'package:opendockify_app/core/models/auth.dart';

void main() {
  group('SettingView.fromJson', () {
    test('parses secret settings with masking', () {
      final setting = SettingView.fromJson(const {
        'key': 'Ai:ApiKey',
        'value': 'sk-1234',
        'source': 'UserSecrets',
        'isSecret': true,
        'masked': true,
      });
      expect(setting.key, 'Ai:ApiKey');
      expect(setting.isSecret, isTrue);
      expect(setting.masked, isTrue);
    });
  });

  group('AiUsageEntry.fromJson', () {
    test('parses usage log', () {
      final entry = AiUsageEntry.fromJson(const {
        'id': 'u1',
        'userId': 'user-1',
        'action': 'polish-clause',
        'requestSnippet': '{"draft":"..."}',
        'responseSnippet': '{"text":"..."}',
        'success': true,
        'timestamp': '2026-01-01T00:00:00Z',
      });
      expect(entry.action, 'polish-clause');
      expect(entry.success, isTrue);
      expect(entry.timestamp, isNotNull);
    });
  });

  group('Polish requests', () {
    test('PolishClauseRequest serializes draft', () {
      final json = const PolishClauseRequest(templateId: 't1', draft: 'draft').toJson();
      expect(json['templateId'], 't1');
      expect(json['draft'], 'draft');
    });

    test('PolishDocumentRequest serializes the full snapshot', () {
      final json = const PolishDocumentRequest(
        templateId: 't1',
        renderedText: 'body',
        values: {'amount': '1000'},
        selectedClauseIds: ['cl1'],
      ).toJson();
      expect(json['templateId'], 't1');
      expect(json['values'], {'amount': '1000'});
      expect(json['selectedClauseIds'], ['cl1']);
    });
  });

  group('AuthResponse.fromJson', () {
    test('parses token response', () {
      final auth = AuthResponse.fromJson(const {
        'id': 'u1',
        'username': 'alice',
        'role': 'User',
        'token': 'jwt.here',
      });
      expect(auth.username, 'alice');
      expect(auth.token, 'jwt.here');
    });
  });

  group('UserProfile.fromJson', () {
    test('detects administrator flag', () {
      final admin = UserProfile.fromJson(const {
        'id': 'u1',
        'username': 'root',
        'displayName': 'Root',
        'role': 'Administrator',
        'isAdministrator': true,
      });
      expect(admin.isAdministrator, isTrue);
      expect(admin.isAdmin, isTrue);
    });

    test('defaults to non-admin', () {
      final user = UserProfile.fromJson(const {});
      expect(user.isAdministrator, isFalse);
    });
  });
}