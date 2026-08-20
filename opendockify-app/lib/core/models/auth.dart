class AuthResponse {
  const AuthResponse({required this.id, required this.username, required this.role, required this.token});

  final String id;
  final String username;
  final String role;
  final String token;

  factory AuthResponse.fromJson(Map<String, dynamic> json) => AuthResponse(
        id: json['id']?.toString() ?? '',
        username: json['username']?.toString() ?? '',
        role: json['role']?.toString() ?? '',
        token: json['token']?.toString() ?? '',
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