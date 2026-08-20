## 1. Templates Module

- [x] 1.1 Create `src/OpenDockify.Templates`; add `Models/Template.cs`
  (Id, OwnerId?, IsBuiltIn, IsPublic, Name, Category, Description,
  RiskNoticeText, Body, DefinitionJson, CreatedAt, UpdatedAt) and
  `Models/TemplateDefinition.cs` (`FieldDefinition`, `ClauseDefinition`,
  `FieldType` enum, `ValidationRule` type)
- [x] 1.2 Add `Configuration/TemplateConfiguration.cs` (owner index, built-in
  flag)
- [x] 1.3 Implement `Services/TemplateDefinitionValidator.cs` — schema check,
  field types, unique clause ids, placeholder→field consistency, size/depth
  caps, unknown-JSON-property rejection
- [x] 1.4 Implement `Services/TemplateRenderer.cs` — `{{field}}` substitution,
  currency → RMB uppercase (delegates to Finance), date formatting, selected
  clause insertion by id, missing-value error
- [x] 1.5 Implement `Services/TemplateService.cs` — CRUD, copy, marketplace
  (public + own), built-in immutability guard, admin public-template upsert
- [x] 1.6 `TemplatesModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Data + Migration

- [x] 2.1 Register `Template` DbSet + configuration in `OpenDockify.Data`
- [x] 2.2 Implement `BuiltInTemplates` static data (8 templates: Loan IOU,
  General IOU, Residential Lease, Mutual NDA, Outsourcing Service,
  Part-time/Labor, Repayment Confirmation, Simple Demand Letter) with proper
  fields/clauses/risk notices
- [x] 2.3 Extend seeder: insert built-ins when DB empty
- [x] 2.4 `dotnet ef migrations add AddTemplates` and apply

## 3. API Endpoints

- [x] 3.1 `GET /api/templates/marketplace` — public + own templates
  (authentication required)
- [x] 3.2 `GET /api/templates/{id}` — own or public; foreign private → 404
- [x] 3.3 `POST /api/templates` — create private template (validate definition;
  size cap; on error 400/413)
- [x] 3.4 `PUT /api/templates/{id}` — update own; built-in or foreign → 403/404
- [x] 3.5 `DELETE /api/templates/{id}` — delete own; built-in or foreign → 403/404
- [x] 3.6 `POST /api/templates/{id}/copy` — deep-clone to a private copy owned
  by the caller (built-ins allowed)
- [x] 3.7 `POST /api/admin/templates` (RequireAdmin) — create/update a public
  global template; built-ins still immutable

## 4. Build & Verify

- [x] 4.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [x] 4.2 HTTP smoke tests:
  - Fresh DB → marketplace shows 8 built-ins
  - Copy built-in → private copy created, editable; original unchanged
  - Update built-in → 403; delete built-in → 403
  - Create private template with valid definition → 200; malformed JSON → 400;
    oversized → 400/413; unknown field type → 400; duplicate clause ids → 400;
    undeclared placeholder → 400
  - User B cannot read/edit/delete user A's private template → 404
  - Renderer unit-checked: currency `1234` → `壹仟贰佰叁拾肆元整`; missing
    value → error
