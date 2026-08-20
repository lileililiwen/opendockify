# OpenDockify App

Flutter client for a self-hosted [OpenDockify](../README.md) server. It lets
users connect to their own server instance, draft documents from shared
templates, and (if the administrator enables it) polish clauses and documents
with AI.

## Features

- **Self-hosted connection**: first-launch screen to configure the server URL
  (validated with `GET /healthz`), persisted across restarts.
- **Authentication**: register / login against the server, JWT kept in the
  platform secure storage (Keystore / Keychain). Sessions survive restarts and
  recover automatically from 401 responses.
- **Templates**: browse the marketplace, view template details, and fill in the
  dynamic form (text / number / currency / date fields and optional clauses)
  with client-side validation mirroring the backend rules. Logged-in users can
  copy and edit templates they own.
- **Documents**: generate, list (paginated), view, download/open/share the
  generated PDF, and re-edit an existing document (creating a new immutable
  version prefilled from its snapshot).
- **AI polish**: one-tap clause/document polishing with a per-session privacy
  disclaimer; the feature hides automatically when disabled by the server.
- **Admin** (for administrators): view/edit server settings, upsert global
  templates, and review AI usage.

## Requirements

- Flutter 3.x (tested with 3.47 / Dart 3.13)
- Android SDK (API 36) or Xcode for iOS
- A running OpenDockify server

## Getting started

```sh
cd opendockify-app
flutter pub get
flutter run          # pick a device/emulator
```

On first launch the app asks for the server URL, e.g. `http://192.168.1.10:8080`.

## Configuration

No build-time configuration is required; the server address is entered at
runtime and stored locally. `TokenStore` (secure storage) and
`AppConfigStore` (shared preferences) are wired in `lib/main.dart` and can be
overridden in tests.

## Testing

```sh
flutter analyze
flutter test
```

- `test/unit/` – controllers and DTO/parser tests with in-memory fakes
  (`SessionController`, `ConnectionController`, template parsing, DTO mapping).
- `test/widget/` – widget tests for the dynamic template form and app bootstrap.
- `test/contract/` – `ApiClient` contract tests against a fake HTTP adapter
  that assert the exact requests (method, path, body) and response parsing.

## Project layout

```
lib/
  core/          api client, config, providers, routing, theme, shared widgets
  features/
    auth/        connection, login, register, splash, session
    templates/   marketplace, detail, editor, dynamic form
    documents/   list, fill/generate, detail, PDF export
    ai/          polish clause/document, consent
    admin/       settings, template upsert, AI usage
    settings/    app settings screen and navigation bar
```

## License

See the repository [LICENSE](../LICENSE).