## Why

The Flutter app has the core document and integration journeys, but the audit
found inconsistent loading, empty, and error states; raw exception text exposed
to users; limited responsive navigation; and no systematic accessibility or
visual regression gate. These are high-risk usability issues for a self-hosted
document tool where recovery and comprehension matter.

## What Changes

- Establish shared screen-state, empty-state, and user-safe error components.
- Make document, template, auth, admin, and integrations flows responsive at
  narrow, wide, text-scaled, dark, and keyboard-navigation configurations.
- Add semantic labels, focus order, visible focus, minimum target sizes, and
  non-color status communication for all interactive controls.
- Add widget and golden coverage for loading, error, empty, busy, selected,
  disabled, dark, and large-text states.

## Capabilities

### New Capabilities

- `ui-ux-accessibility`: coherent responsive and accessible Flutter journeys.

## Non-goals

- No backend API redesign, product feature expansion, or new visual brand.
- No animation overhaul beyond reduced-motion-safe feedback required for states.

## Impact

Changes are limited to `opendockify-app/lib/`, shared test helpers, and
`opendockify-app/test/`. Existing routes and API contracts remain compatible.
