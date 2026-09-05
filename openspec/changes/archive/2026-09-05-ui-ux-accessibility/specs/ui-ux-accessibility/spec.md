## ADDED Requirements

### Requirement: Consistent recoverable screen states

Every routed data screen SHALL render explicit loading, success, empty, and
recoverable error states, and SHALL present user-safe error copy with a retry
action where retry is meaningful.

#### Scenario: API failure on a list screen

- **WHEN** a document, template, token, webhook, or delivery request fails
- **THEN** the screen shows stable explanatory copy, preserves the current
  filter/input state, and exposes a retry action without displaying exception
  type names, stack traces, or raw server payloads

### Requirement: Responsive accessible interaction

Interactive controls SHALL have an accessible name, visible focus, keyboard
reachability, a target of at least 24x24 logical pixels, and status cues that
do not depend on color alone. Layouts SHALL remain usable at 320dp width and
200% text scale.

#### Scenario: Keyboard user completes a document action

- **WHEN** a keyboard user tabs through a document form and chooses preview or
  finalize
- **THEN** focus order is logical, focused controls are visible and unobscured,
  validation is announced, and the busy state disables duplicate submission

### Requirement: State and theme regression coverage

The app SHALL have automated widget/golden coverage for semantic-changing
states across light/dark themes, narrow/wide breakpoints, RTL, and text scale.

#### Scenario: Visual state regression is introduced

- **WHEN** a change clips a state at a supported breakpoint or removes a
  required semantic label
- **THEN** the relevant automated test fails before the change is accepted
