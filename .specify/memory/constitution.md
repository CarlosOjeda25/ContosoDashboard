<!--
Sync Impact Report
Version change: UNSET → 1.0.0
New file — first full ratification of ContosoDashboard Constitution.

Principles established:
  - I.  Pila Tecnológica Unificada (.NET 10 / Blazor)
  - II. Arquitectura Limpia y Patrones de Diseño
  - III. Estándares de Codificación
  - IV. Seguridad y Control de Acceso — NON-NEGOTIABLE
  - V.  Aseguramiento de la Calidad y Testing — NON-NEGOTIABLE
  - VI. Rendimiento y Escalabilidad

Templates reviewed:
  - .specify/templates/plan-template.md   ✅ Constitution Check placeholder is generic; will be filled per feature
  - .specify/templates/spec-template.md   ✅ no change required
  - .specify/templates/tasks-template.md  ✅ no change required

Deferred items: None.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Pila Tecnológica Unificada (Tech Stack)

**Non-negotiable.** Every service, module, and feature in ContosoDashboard MUST conform to the following
stack. Deviations require written approval from the Architecture Board before any code is written.

- **Runtime**: .NET 10 (LTS). Downgrading to earlier TFMs or introducing side-car runtimes per service
  is prohibited without Architecture Board approval.
- **Language**: C# 13+. Use of VB.NET, F#, or any other .NET language is prohibited unless explicitly
  approved.
- **ORM**: Entity Framework Core (latest stable for .NET 10). Raw ADO.NET is only permitted for
  performance-critical paths that are documented, justified, and peer-reviewed.
- **UI Framework**: Blazor (Server or WebAssembly as appropriate per deployment context). Introduction of
  external JavaScript SPA frameworks — React, Angular, Vue, Svelte, or equivalent — is PROHIBITED.
  Vanilla JavaScript / TypeScript interop is allowed exclusively for browser-specific capabilities not
  covered by Blazor's component model.
- **API Layer**: ASP.NET Core (Minimal APIs or Controllers). gRPC is permitted for internal
  service-to-service communication.
- **Package Management**: NuGet only. Bundled or vendored third-party DLLs are not permitted without a
  security review sign-off.

### II. Arquitectura y Patrones de Diseño

ContosoDashboard MUST maintain Clean Architecture (Onion / N-Tier) with strict, enforced layer
separation. Layer violations are grounds for PR rejection.

**Layer boundaries**:

| Layer | Responsibility | May depend on |
|---|---|---|
| Domain | Entities, value objects, domain events, domain exceptions | Nothing |
| Application | Use cases, service interfaces, DTOs, command/query handlers | Domain |
| Infrastructure | EF Core DbContext, repositories, file adapters, external integrations | Application, Domain |
| Presentation | Blazor pages, components, view models | Application |

**Mandatory patterns**:

- **Repository Pattern**: All data access MUST go through repository interfaces declared in Application
  and implemented in Infrastructure. Direct `DbContext` calls from pages, components, or application
  services are PROHIBITED.
- **Dependency Injection**: Only the built-in .NET DI container (`Microsoft.Extensions.DependencyInjection`)
  is used. Service-locator anti-pattern (`IServiceProvider` resolved inside business logic) is prohibited.
  All registrations live in `Program.cs` or dedicated `IServiceCollection` extension methods.
- **CQRS**: Commands (write operations) and Queries (read operations) MUST be separated in handler
  classes. MediatR or an equivalent in-process mediator MUST be used for dispatch. Mixing read and
  write logic in a single handler is prohibited.

### III. Estándares de Codificación

All code MUST conform to Microsoft C# Coding Conventions plus the following project-specific rules.
The authoritative formatter configuration is `.editorconfig` at repository root; CI enforces it via
`dotnet format --verify-no-changes`.

**Naming conventions**:

- `PascalCase`: Classes, interfaces (`IDocumentRepository`), methods, properties, enums, events,
  constants.
- `camelCase`: Local variables, method parameters, private fields (prefix `_`, e.g.
  `_documentRepository`).
- `SCREAMING_SNAKE_CASE` is not used in C#. Use `public const string` with PascalCase.

**Error handling**:

- Centralized exception handling MUST be implemented via ASP.NET Core middleware (`IExceptionHandler`
  or `UseExceptionHandler`). No bare `try/catch` at controller or page boundaries for general-purpose
  error surfacing.
- Domain exceptions MUST extend a project-level `DomainException` base class. Infrastructure exceptions
  MUST be caught at the Infrastructure boundary, logged, and re-thrown as domain exceptions when
  appropriate.
- Silent exception swallowing (`catch (Exception) { }`) is PROHIBITED.

**Logging**:

- Declare `ILogger<T>` via constructor injection in every service. Serilog MUST be configured as the
  concrete provider with at minimum Console and rolling-File sinks in all environments and Azure
  Application Insights in production.
- Log levels MUST be used semantically: `Debug`/`Trace` for diagnostics, `Information` for business
  events, `Warning` for recoverable anomalies, `Error` for failures, `Critical` for system-wide
  failures.
- Sensitive data — passwords, file content, PII — MUST NEVER appear in log output. Destructure only
  identifiers (e.g., `{DocumentId}`), never payloads.

**Language features**:

- Nullable reference types (`<Nullable>enable</Nullable>`) are enabled project-wide. Suppression
  (`#nullable disable`) requires an inline comment explaining why.
- `async`/`await` MUST be used for all I/O-bound operations. `.Result`, `.Wait()`, and
  `.GetAwaiter().GetResult()` are PROHIBITED. `async void` is only permitted for Blazor event handlers.

### IV. Seguridad y Control de Acceso (RBAC) — NON-NEGOTIABLE

Violations of rules in this section are BLOCKING for production deployment. No exceptions without a
documented, Architecture-Board-approved security exception.

**Authentication & Authorization**:

- Authentication: ASP.NET Core Identity with JWT Bearer tokens for API endpoints and Cookie
  authentication for Blazor Server sessions.
- Authorization: Role-Based Access Control (RBAC) enforced via `[Authorize(Roles = "...")]` attributes
  AND policy-based checks (`IAuthorizationService`) for fine-grained resource-level decisions.
- Authorization MUST be verified at the **service layer**, not solely at the UI or controller layer.
  A document MUST only be accessible when the calling user's role explicitly grants read access to
  that document's category.

**Document Upload Security** (Document Management Module):

- **MIME Validation**: File type MUST be validated server-side by inspecting the file's **magic bytes**
  (file signature), NOT the `Content-Type` header or the file extension alone. Permitted types: PDF,
  DOCX, XLSX, PPTX, PNG, JPEG. Any other type MUST be rejected with `400 Bad Request`.
- **File Size Limit**: Maximum upload size is **50 MB** per file. Enforced at two levels:
  (1) Blazor component level for UX feedback; (2) ASP.NET Core middleware (`MaxRequestBodySize`)
  as the hard server-side gate. Requests exceeding the limit MUST be rejected with
  `413 Request Entity Too Large`.
- **Malware Scanning**: Every uploaded file MUST be passed through a malware/antivirus scanning
  service (abstracted behind `IAntivirusScanner`) **before** the file is persisted to any storage
  path accessible by the application. Files flagged as infected or suspicious MUST be quarantined,
  never stored in the accessible path, and an alert MUST be emitted at `Critical` log level.
- **Storage Isolation**: Uploaded files MUST be stored **outside the web root**. Direct URL access to
  raw stored files is PROHIBITED. Files are served exclusively through authenticated controller
  endpoints that re-verify RBAC before streaming the response.
- **File Naming on Disk**: Stored files MUST use a GUID-based name generated by the application.
  The user-supplied original filename MUST NEVER be used on disk. The original filename is stored
  only in the database.
- **Input Sanitization**: All document metadata (title, description, tags) MUST be sanitized
  server-side. Parameterized queries (EF Core default) are mandatory; raw SQL string interpolation
  is PROHIBITED.

### V. Aseguramiento de la Calidad y Testing — NON-NEGOTIABLE

**No code may be merged to `main` or deployed to production without passing all automated test gates.**

- **Unit Tests**: xUnit is the primary framework. Moq or NSubstitute for mocking external dependencies.
  Minimum **80% line coverage** across Application and Domain layers, measured by Coverlet and
  enforced as a CI quality gate. Falling below threshold BLOCKS the pipeline.
- **Integration Tests**: Required for all repository implementations, file upload pipelines, and RBAC
  authorization policies. Use `WebApplicationFactory<Program>` with Testcontainers (SQL Server or
  PostgreSQL) for a production-equivalent database. Document upload integration tests MUST cover at
  minimum: valid upload, oversized file rejection, invalid MIME rejection, malware detection trigger.
- **UI / Component Tests**: Blazor components MUST be covered by bUnit tests for rendering logic,
  user interactions, and state transitions. Snapshot tests are required for critical layout
  components.
- **Test Project Structure**:
  ```
  ContosoDashboard.Tests.Unit/
  ContosoDashboard.Tests.Integration/
  ContosoDashboard.Tests.UI/
  ```
  No test code in production projects.
- **CI Gate**: Every PR MUST pass: `dotnet test` (all suites) + Coverlet threshold + `dotnet format
  --verify-no-changes`. A failing CI pipeline BLOCKS merge without exception.
- **Test Naming Convention**: `MethodName_Scenario_ExpectedBehavior`
  (e.g., `UploadDocument_ExceedsMaxSize_Returns413`).
- Mocking the system under test is prohibited. Only external dependencies (repositories, external
  HTTP services, file scanners) may be mocked.

### VI. Rendimiento y Escalabilidad

- **Pagination**: All Blazor pages and API endpoints that list documents or records MUST implement
  server-side pagination. Default page size: **20** items. Maximum page size: **100** items.
  Queries MUST use `Skip`/`Take` with `AsNoTracking()` for read-only projections.
- **Async I/O**: ALL database calls, file I/O, HTTP client calls, and external service integrations
  MUST use `async`/`await` throughout the entire call stack. Blocking calls are PROHIBITED.
- **Lazy Loading**: EF Core lazy loading is **disabled** project-wide (`UseLazyLoadingProxies()` MUST
  NOT be called). Eager loading (`Include`/`ThenInclude`) or explicit loading (`LoadAsync`) MUST be
  used deliberately and documented in the query. Over-fetching entire entity graphs when only scalar
  identifiers are required is a mandatory code-review concern.
- **Caching**: `IMemoryCache` or Output Caching MAY be used for stable reference data (user roles,
  document categories). Cached entries MUST carry an explicit expiration policy. Caching of
  user-specific or authorization-sensitive data MUST use per-user cache keys scoped to the user's
  identity claim.
- **File Streaming**: Document downloads MUST use `FileStreamResult` or `Stream`-based responses.
  Loading an entire document file into a `byte[]` or `string` in memory is PROHIBITED.
- **Database Indexes**: All foreign keys and columns used in `WHERE` or `ORDER BY` clauses for
  document queries MUST have corresponding database indexes. Migrations that add filterable columns
  without an index will be rejected in code review.

## Security Compliance Checklist

Every PR that touches the Document Management module MUST include sign-off on the following items
in the PR description before it can be approved:

- [ ] MIME magic-byte validation implemented and covered by a unit test
- [ ] File size limit enforced at ASP.NET Core middleware level (not UI only)
- [ ] `IAntivirusScanner` invoked before any storage write; scan failure quarantines the file
- [ ] RBAC authorization check performed at the service layer (not UI only)
- [ ] Stored filename is application-generated GUID; original name stored in DB only
- [ ] No sensitive data (file content, PII) appears in log output
- [ ] Nullable reference types enabled; no suppression without inline justification
- [ ] `dotnet format` passes; no warnings introduced

## Development Workflow & Quality Gates

1. **Feature branches** follow `###-feature-name` naming (e.g., `001-document-upload`).
2. **PR Requirements**: Every PR MUST link to a `spec.md` and a `plan.md` in the corresponding
   `/specs/###-feature-name/` folder.
3. **Code Review**: Changes to Domain or Infrastructure layers require at least 1 approver from the
   Architecture Board. All other changes require at least 1 peer review.
4. **Definition of Done**: CI green + coverage ≥ 80% + Security Checklist signed off + reviewed +
   approved.
5. **Breaking Changes**: Any breaking change to a public contract (API endpoint signature, repository
   interface, domain event schema) requires a MAJOR version bump in the affected module and an
   accompanying migration guide.
6. **Hotfixes**: Branches named `hotfix/###-description` are exempt from the spec requirement but MUST
   include a regression test that reproduces the bug before the fix is applied.

## Governance

This constitution supersedes all prior verbal agreements, wiki pages, or informal coding standards for
ContosoDashboard. It is enforced programmatically (CI pipeline) and by mandatory code review.

**Amendment Procedure**:

1. Raise a proposal in the Architecture Board meeting or as a GitHub Discussion tagged
   `constitution-amendment`.
2. Proposal MUST include: motivation, impact assessment on existing code, and migration plan.
3. Approved by Architecture Board majority with Tech Lead sign-off.
4. Version bumped per the Semantic Versioning policy below. `LAST_AMENDED_DATE` updated and a
   changelog entry added at the bottom of this file.

**Versioning Policy**:

- **MAJOR**: Removal or fundamental redefinition of a principle; backward-incompatible architectural change.
- **MINOR**: New principle or significant new section added; materially expanded guidance.
- **PATCH**: Clarifications, wording improvements, typo fixes — no semantic change.

**Compliance Review**: The Architecture Board conducts a quarterly compliance review. Non-compliant code
identified during review MUST be remediated within the following sprint. Repeated non-compliance by an
individual triggers a performance conversation.

---

**Version**: 1.0.0 | **Ratified**: 2026-06-11 | **Last Amended**: 2026-06-11
