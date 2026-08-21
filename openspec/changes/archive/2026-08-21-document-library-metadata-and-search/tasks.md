## 1. Tests First

- [x] 1.1 Add backend tests for query parsing, metadata validation, title fallback,
  and version connectivity
- [x] 1.2 Extend Flutter DTO and API contract tests for library query and metadata
  contracts
- [x] 1.3 Define owner-isolation, search/filter/sort, metadata, and history HTTP
  smoke scenarios

## 2. Persistence and Domain Service

- [x] 2.1 Add title/archive fields, constraints, indexes, and a provider-compatible
  EF migration
- [x] 2.2 Add bounded library query parsing and projected paginated summaries
- [x] 2.3 Implement owner-scoped metadata mutation and connected version history
- [x] 2.4 Assign initial titles and inherit titles during immutable re-edit

## 3. API

- [x] 3.1 Extend list/detail responses with library metadata and template name
- [x] 3.2 Add metadata update and version-history endpoints with validation and
  owner-hiding behavior

## 4. Flutter Application

- [x] 4.1 Extend document models and API client methods for library metadata,
  queries, and history
- [x] 4.2 Add controller query state, pagination reset, metadata actions, and
  version loading
- [x] 4.3 Upgrade the document list with search, archive filter, sort controls, and
  meaningful summary rows
- [x] 4.4 Add rename, archive/restore, and version-history actions to document detail

## 5. Verify and Deliver

- [x] 5.1 Validate OpenSpec strictly and run backend formatting/diff checks
- [x] 5.2 Build with zero warnings/errors and run backend and Flutter test suites
- [x] 5.3 Apply migration and execute all HTTP smoke scenarios
- [x] 5.4 Archive the completed change and commit only its related paths
