## 1. Audit contract and shared states

- [x] 1.1 Inventory every routed screen and document loading, empty, error, busy, disabled, and destructive states.
- [x] 1.2 Add shared state widgets and map `ApiError` kinds to safe copy without rendering exception strings.
- [x] 1.3 Add reusable semantics and focus-visible test helpers.

## 2. Screen remediation

- [x] 2.1 Migrate auth, connection, templates, documents, guided interview, admin, and integrations screens to shared states.
- [x] 2.2 Add adaptive navigation and bounded layouts for 320dp, 600dp, and desktop widths without horizontal clipping.
- [x] 2.3 Add labels, tooltips, minimum targets, focus order, and non-color status text to all icon and state controls.
- [x] 2.4 Ensure destructive actions expose consequence, cancellation, and completion feedback.

## 3. Verification

- [x] 3.1 Add widget tests for every state and route guard, including offline/401/server-error recovery.
- [x] 3.2 Add golden tests for light/dark, narrow/wide, RTL, and text scale 2.0.
- [x] 3.3 Run `flutter analyze`, `flutter test`, and the manual keyboard/screen-reader/reduced-motion checklist.
