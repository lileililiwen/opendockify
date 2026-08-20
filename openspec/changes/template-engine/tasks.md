## 1. Templates Module

- [ ] 1.1 Create `src/OpenDockify.Templates`; add `Models/Template.cs`
  (Id, OwnerId?, IsBuiltIn, IsPublic, Name, Category, Description,
  RiskNoticeText, Body, DefinitionJson, CreatedAt, UpdatedAt) and
  `Models/TemplateDefinition.cs` (`FieldDefinition`, `ClauseDefinition`,
  `FieldType` enum, `ValidationRule` type)
- [ ] 1.2 Add `Configuration/TemplateConfiguration.cs` (owner index, built-in
  flag)
- [ ] 1.3 Implement `Services/TemplateDefinitionValidator.cs` — schema check,
  field types, unique clause ids, placeholder→field consistency, size/depth
  caps, unknown-JSON-property rejection
- [ ] 1.4 Implement `Services/TemplateRenderer.cs` — `{{field}}` substitution,
  currency → RMB uppercase (delegates to Finance), date formatting, selected
  clause insertion by id, missing-value error
- [ ] 1.5 Implement `Services/TemplateService.cs` — CRUD, copy, marketplace
  (public + own), built-in immutability guard, admin public-template upsert
- [ ] 1.6 `TemplatesModuleExtensions.cs`; wire into `OpenDockify.Api`

## 2. Data + Migration

- [ ] 2.1 Register `Template` DbSet + configuration in `OpenDockify.Data`
- [ ] 2.2 Implement `BuiltInTemplates` static data (8 templates: Loan IOU,
  General IOU, Residential Lease, Mutual NDA, Outsourcing Service,
  Part-time/Labor, Repayment Confirmation, Simple Demand Letter) with proper
  fields/clauses/risk notices
- [ ] 2.3 Extend seeder: insert built-ins when DB empty
- [ ] 2.4 `dotnet ef migrations add AddTemplates` and apply

## 3. API Endpoints

- [ ] 3.1 `GET /api/templates/marketplace` — public + own templates
  (authentication required)
- [ ] 3.2 `GET /api/templates/{id}` — own or public; foreign private → 404
- [ ] 3.3 `POST /api/templates` — create private template (validate definition;
  size cap; on error 400/413)
- [ ] 3.4 `PUT /api/templates/{id}` — update own; built-in or foreign → 403/404
- [ ] 3.5 `DELETE /api/templates/{id}` — delete own; built-in or foreign → 403/404
- [ ] 3.6 `POST /api/templates/{id}/copy` — deep-clone to a private copy owned
  by the caller (built-ins allowed)
- [ ] 3.7 `POST /api/admin/templates` (RequireAdmin) — create/update a public
  global template; built-ins still immutable

## 4. Build & Verify

- [ ] 4.1 `dotnet build OpenDockify.sln` → 0 warnings / 0 errors
- [ ] 4.2 HTTP smoke tests:
  - Fresh DB → marketplace shows 8 built-ins
  - Copy built-in → private copy created, editable; original unchanged
  - Update built-in → 403; delete built-in → 403
  - Create private template with valid definition → 200; malformed JSON → 400;
    oversized → 400/413; unknown field type → 400; duplicate clause ids → 400;
    undeclared placeholder → 400
  - User B cannot read/edit/delete user A's private template → 404
  - Renderer unit-checked: currency `1234` → `壹仟贰佰叁拾肆元整`; missing
    value → error
