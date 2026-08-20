# flutter-app-ai-assist Specification

## Purpose
TBD - created by archiving change flutter-mobile-app. Update Purpose after archive.

## Requirements

### Requirement: Clause polish

The app SHALL offer polishing a user-drafted clause via
`POST /api/ai/polish-clause`, sending `templateId` and the draft text, and SHALL
display the polished text returned by the API.

#### Scenario: Polish a clause

- **WHEN** the user taps "Polish" on a clause draft
- **THEN** the polished text is returned and shown, and the user can accept it
  into the clause or discard it

#### Scenario: Polish failure

- **WHEN** the API returns an error
- **THEN** the app shows the backend error and keeps the original draft

### Requirement: Document polish

The app SHALL offer polishing the full rendered document via
`POST /api/ai/polish-document`, sending `templateId`, `renderedText`, `values`,
and `selectedClauseIds`, and SHALL display the polished text for review.

#### Scenario: Polish a document

- **WHEN** the user taps "Polish document" on a generated document
- **THEN** the polished text is returned and shown for review; the user can
  accept it (viewing as a reference copy) or discard it

### Requirement: Sensitivity and privacy warning

Every AI-polish entry point in the app SHALL show the privacy warning that
AI polish is best-effort and non-binding, and that submitted text may be sent to
a third-party LLM endpoint configured by the deployer. The app SHALL advise
users not to include personal or identifying data when the endpoint is not a
local Ollama instance.

#### Scenario: Warning shown before use

- **WHEN** the user first uses an AI-polish feature
- **THEN** the privacy/sensitivity warning is displayed before the request is
  made

### Requirement: AI disabled and rate-limit handling

The app SHALL handle `403 Forbidden` (AI disabled) and `429 Too Many Requests`
(rate limit) responses from the AI endpoints with clear, specific messages and
SHALL NOT retry automatically.

#### Scenario: AI disabled

- **WHEN** the API returns `403` with the "AI disabled" error
- **THEN** the app shows "AI is disabled by the administrator" and offers no
  further AI actions in that session

#### Scenario: Rate limited

- **WHEN** the API returns `429`
- **THEN** the app shows "daily AI usage limit reached" and disables further AI
  actions until the user leaves the screen