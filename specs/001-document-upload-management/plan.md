# Implementation Plan: Document Upload and Management

**Branch**: `001-document-upload-management` | **Date**: 2026-06-12 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-document-upload-management/spec.md`

## Summary

Se implementa el modulo de gestion y carga de documentos en ContosoDashboard sobre .NET 10 + Blazor + EF Core,
con almacenamiento inicial en File System local fuera de `wwwroot`, arquitectura limpia por capas,
pipeline de upload fail-closed (streaming, magic-bytes, limite de 25 MB, antivirus sincrono),
hard-delete seguro y trazabilidad de escritura via `DocumentAuditLog` sin penalizar lecturas.

## Technical Context

**Language/Version**: C# 13 sobre .NET 10 (LTS)
**Primary Dependencies**: ASP.NET Core/Blazor, Entity Framework Core, ASP.NET Core Identity, MediatR, ILogger/Serilog, bUnit, xUnit, WebApplicationFactory
**Storage**: SQL Server (metadatos) + File System local fuera de web root (`AppData/uploads`) con rutas GUID; extensible a Azure Blob via `IFileStorageService`
**Testing**: xUnit + bUnit + pruebas de integracion (WebApplicationFactory + DB real de prueba)
**Target Platform**: Windows/Linux server para ASP.NET Core
**Project Type**: Web application monolitica (Blazor + servicios de aplicacion)
**Performance Goals**:
- Upload <= 30s para archivos hasta 25 MB
- Listas y busquedas <= 2s para bibliotecas hasta 500 documentos
- Paginacion server-side (20 por defecto, 100 maximo)
**Constraints**:
- Prohibido cargar archivo completo en `byte[]` para upload/download
- Validacion por magic-bytes y MIME permitidos
- Antivirus obligatorio (fail-closed)
- No frameworks JS externos
- Notificaciones best-effort
**Scale/Scope**:
- Usuarios internos de Contoso (Employee, Team Lead, Project Manager, Administrator)
- Gestion documental por usuario, proyecto y comparticion controlada
- Sin cuotas por usuario/proyecto en v1

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Gate

- ✅ Stack alineado: .NET 10 + C# + EF Core + Blazor
- ✅ Clean Architecture/N-Tier respetada (Domain/Application/Infrastructure/Presentation)
- ✅ Patron Repository + DI nativo obligatorio en el diseno
- ✅ Seguridad upload: magic-bytes, 25 MB (<= 50 MB constitucional), antivirus fail-closed
- ✅ Almacenamiento fuera de web root con GUID
- ✅ RBAC aplicado en capa servicio
- ✅ Estrategia QA definida con cobertura >= 80% y pruebas integracion/upload
- ✅ Performance: paginacion server-side, async end-to-end, streaming

**Resultado**: PASS. Se puede ejecutar Fase 0 y Fase 1.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-upload-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── document-management-api.yaml
└── tasks.md            # generado por /speckit.tasks (fuera de este comando)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── (nuevo) DocumentRepository.cs
│   └── (nuevo) DocumentAuditLogRepository.cs
├── Models/
│   ├── User.cs
│   ├── Project.cs
│   ├── TaskItem.cs
│   └── (nuevo) Document*.cs
├── Services/
│   ├── ProjectService.cs
│   ├── TaskService.cs
│   ├── (nuevo) IDocumentRepository.cs
│   ├── (nuevo) IDocumentAuditLogRepository.cs
│   └── Documents/
│       ├── Commands/    (nuevo) — command records + IRequestHandler<> implementations
│       └── Queries/     (nuevo) — query records + IRequestHandler<> implementations
├── Pages/
│   ├── Projects.razor
│   ├── ProjectDetails.razor
│   ├── Tasks.razor
│   └── (nuevo) Documents*.razor
├── Shared/
├── Program.cs
└── wwwroot/

tests/
├── ContosoDashboard.Tests.Unit/
├── ContosoDashboard.Tests.Integration/
└── ContosoDashboard.Tests.UI/
```

**Structure Decision**: Se mantiene estructura web actual de ContosoDashboard (monolito Blazor + servicios),
agregando el bounded context de Document Management en `Models/`, `Services/`, `Pages/` y tests separados por tipo.

**CQRS + Repository Pattern** (constitución §II — obligatorio):
- `Services/Documents/Commands/` → command records + `IRequestHandler<TCommand, TResult>` — operaciones de escritura
- `Services/Documents/Queries/` → query records + `IRequestHandler<TQuery, TResult>` — operaciones de lectura
- `Services/IDocumentRepository.cs` y `Services/IDocumentAuditLogRepository.cs` → interfaces (capa Application)
- `Data/DocumentRepository.cs` y `Data/DocumentAuditLogRepository.cs` → implementaciones EF Core (capa Infrastructure)
- Blazor pages y controllers despachan exclusivamente vía `IMediator.Send()` — NUNCA instancian handlers directamente

## Phase 0: Research Output Plan

`research.md` consolida decisiones cerradas (sin `NEEDS CLARIFICATION`):

1. Estrategia de almacenamiento local + abstraccion `IFileStorageService`
2. Pipeline upload secuencial fail-closed con stream
3. Hard-delete seguro DB + file system
4. Auditoria de escritura (`DocumentAuditLog`) sin impacto en lectura
5. Notificaciones best-effort

## Phase 1: Design & Contracts Output Plan

1. `data-model.md`
  - Entidades `Document`, `DocumentTag`, `DocumentShare`, `DocumentAuditLog`
  - Tipos C#, relaciones y restricciones
  - Fluent API (indices, longitudes, FKs, delete behaviors)
2. `contracts/document-management-api.yaml`
  - Contrato REST para upload/download/preview/search/share/delete/audit
  - Reglas de seguridad y codigos de error
3. `quickstart.md`
  - Flujo de implementacion incremental + verificacion de calidad
  - Casos de prueba de pipeline upload y RBAC
4. Actualizacion de contexto agente (`.github/copilot-instructions.md`)

## Post-Design Constitution Re-Check

- ✅ `IFileStorageService` + diseño migrable a Azure Blob sin acoplar UI/negocio
- ✅ Upload pipeline stream-first, validacion stricta y antivirus sync fail-closed
- ✅ Hard-delete confirmado (purgado DB + file)
- ✅ `DocumentAuditLog` solo en eventos write (Upload/Delete/Replace/Share)
- ✅ Lecturas optimizadas sin carga de auditoria en queries de browse/search
- ✅ Paginacion, async I/O, RBAC en capa servicio, y trazabilidad de seguridad

**Resultado**: PASS. Fase de planning completada y lista para `/speckit.tasks`.

## Architecture Decisions

### AD-001 — RecipientTeamId: definición de "equipo" en v1

**Decisión**: En el modelo de datos actual de ContosoDashboard no existe una entidad `Team` independiente.
Para esta iteración, `DocumentShare.RecipientTeamId` se interpreta como el identificador de un **proyecto** (`Project.Id`).
"Compartir con un equipo" = compartir con todos los `ProjectMember` activos del proyecto indicado.

**Contrato de implementación** (T-031):
- `RecipientTeamIds` en el command contiene `Project.Id` values
- El handler resuelve los `ProjectMember.UserId` de cada proyecto y crea un `DocumentShare` por miembro activo
- No se introduce ninguna entidad `Team` nueva en esta iteración
- `DocumentShare.RecipientTeamId` almacena el `ProjectId` hasta que se introduzca una entidad `Team` propia en v2

**Rationale**: Mantener el alcance de v1 sin nuevas entidades. La semántica de "equipo" queda cubierta
por la membresía de proyecto existente (`ProjectMember`). Migración limpia posible si se introduce `Team` en v2.

## Complexity Tracking

No hay violaciones de constitucion que requieran excepcion.
