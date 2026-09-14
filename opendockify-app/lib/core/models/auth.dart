class AuthResponse {
  const AuthResponse({
    required this.id,
    required this.username,
    required this.role,
    required this.token,
    this.refreshToken,
    this.mode,
    this.challengeId,
  });

  final String id;
  final String username;
  final String role;
  final String token;

  /// Opaque refresh handle (login / refresh / 2FA-verify responses).
  final String? refreshToken;

  /// Login mode: `authenticated` or `2fa-required`.
  final String? mode;

  /// 2FA challenge id when [mode] is `2fa-required`.
  final String? challengeId;

  bool get requiresTwoFactor => mode == '2fa-required';

  factory AuthResponse.fromJson(Map<String, dynamic> json) => AuthResponse(
        id: json['id']?.toString() ?? json['userId']?.toString() ?? '',
        username: json['username']?.toString() ?? '',
        role: json['role']?.toString() ?? '',
        token: json['token']?.toString() ?? '',
        refreshToken: json['refreshToken']?.toString(),
        mode: json['mode']?.toString(),
        challengeId: json['challengeId']?.toString(),
      );
}

class UserProfile {
  const UserProfile({
    required this.id,
    required this.username,
    required this.displayName,
    required this.role,
    required this.isAdministrator,
  });

  final String id;
  final String username;
  final String displayName;
  final String role;
  final bool isAdministrator;

  bool get isAdmin => isAdministrator;

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
        id: json['id']?.toString() ?? '',
        username: json['username']?.toString() ?? '',
        displayName: json['displayName']?.toString() ?? '',
        role: json['role']?.toString() ?? '',
        isAdministrator: json['isAdministrator'] == true,
      );
}