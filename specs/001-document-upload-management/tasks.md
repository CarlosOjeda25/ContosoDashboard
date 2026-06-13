---
description: "Task list definitivo — Document Upload and Management (post-auditoría v2)"
---

# Tasks: Document Upload and Management

**Rama**: `001-document-upload-management` | **Fecha**: 2026-06-12 | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Estado**: ✅ Listo para implementación — remediaciones C1, C2, I1, G1, G2 incorporadas
**Convención de IDs**: `T-NNN` secuencial, agrupado por fase F1–F6

---

## Cómo leer este documento

Cada tarea sigue el esquema:

```
- [ ] T-NNN: Descripción concisa
  - **Depende de**: T-XXX (vacío = sin dependencias)
  - **Criterios de aceptación**:
    - Criterio 1
```

**Estimación**: cada tarea cabe en 2 h–1 día. Las marcadas `[P]` son paralelizables una vez resueltas sus dependencias.

---

## Fase 1 — Configuración e Infraestructura

> Scaffolding: proyectos de test, paquetes NuGet, configuración del servidor.
> Sin esta fase no se puede compilar ni testear el resto del sistema.

---

- [X] T-001: Crear los tres proyectos de test en la solución: `ContosoDashboard.Tests.Unit`, `ContosoDashboard.Tests.Integration` y `ContosoDashboard.Tests.UI`
  - **Depende de**: —
  - **Criterios de aceptación**:
    - Los tres `.csproj` existen bajo `tests/` en la raíz de la solución
    - `dotnet build` compila los tres proyectos sin errores
    - No hay código de producción embebido en los proyectos de test

- [X] T-002 [P]: Agregar dependencias de test a `ContosoDashboard.Tests.Unit` — `xUnit`, `Moq`, `Coverlet.Collector`; configurar umbral de cobertura ≥ 80 % en el `.csproj`
  - **Depende de**: T-001
  - **Criterios de aceptación**:
    - `Coverlet.Collector` presente con colector habilitado
    - `<ThresholdType>line</ThresholdType><Threshold>80</Threshold>` configurado para el runner
    - `dotnet test --collect:"XPlat Code Coverage"` reporta cobertura sin error de compilación

- [X] T-003 [P]: Agregar dependencias a `ContosoDashboard.Tests.Integration` — `xUnit`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.SqlServer`
  - **Depende de**: T-001
  - **Criterios de aceptación**:
    - `WebApplicationFactory<Program>` compila sin error de referencia
    - `Testcontainers.SqlServer` referenciado y el namespace resuelve

- [X] T-004 [P]: Agregar dependencias a `ContosoDashboard.Tests.UI` — `xUnit`, `bunit`
  - **Depende de**: T-001
  - **Criterios de aceptación**:
    - `Bunit` instalado y el namespace `Bunit` resuelve en un archivo de test vacío

- [X] T-005: Agregar paquete `MediatR` al proyecto principal `ContosoDashboard.csproj` y registrar `builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))` en `ContosoDashboard/Program.cs`
  - **Depende de**: —
  - **Criterios de aceptación**:
    - `<PackageReference Include="MediatR" />` presente
    - `AddMediatR` invocado antes de `builder.Build()`
    - `dotnet build` compila sin advertencias de MediatR

- [X] T-006: Configurar `MaxRequestBodySize` a 25 MB en Kestrel (`26_214_400` bytes) en `ContosoDashboard/Program.cs`; crear el directorio `AppData/uploads` fuera de `wwwroot` si no existe al iniciar la aplicación
  - **Depende de**: —
  - **Criterios de aceptación**:
    - `options.Limits.MaxRequestBodySize = 26_214_400` presente en la configuración de Kestrel
    - El directorio `AppData/uploads` es creado al inicio si no existe, sin excepción
    - Subir un archivo de 26 MB retorna `413 Request Entity Too Large`

- [X] T-007: Configurar el middleware de manejo centralizado de excepciones en `ContosoDashboard/Program.cs` (`UseExceptionHandler` / `IExceptionHandler`) que traduzca `ForbiddenException` → 403, `DocumentNotFoundException` → 404, `DocumentUploadException` → 400/413/503 según su código interno
  - **Depende de**: —
  - **Criterios de aceptación**:
    - Ningún `catch` genérico en controllers o páginas Blazor para errores de dominio
    - Las excepciones de dominio producen el código HTTP correcto verificado en tests de integración
    - Las excepciones no manejadas retornan 500 con body estandarizado (sin stack trace en producción)

- [X] T-066: Instalar paquetes Serilog y configurar logging estructurado en `ContosoDashboard/Program.cs` — constitución §III
  - **Depende de**: —
  - **Criterios de aceptación**:
    - `<PackageReference>` para `Serilog.AspNetCore`, `Serilog.Sinks.File`, `Serilog.Sinks.Console`, `Serilog.Sinks.ApplicationInsights` presentes en `ContosoDashboard.csproj`
    - `Host.UseSerilog(...)` configurado en `Program.cs` antes de `builder.Build()`
    - Console sink + rolling-File sink (`logs/contoso-.txt`) activos en todos los entornos
    - Azure Application Insights sink activado condicionalmente solo para `ASPNETCORE_ENVIRONMENT == Production`
    - Log level `Information` por defecto; `Debug` en Development vía `appsettings.Development.json`
    - PII (contenido de archivo, rutas físicas, datos personales) NUNCA aparece en logs — solo `{DocumentId}`, `{UserId}`, `{EventType}`
    - `dotnet build` compila sin warnings de Serilog

- [X] T-067: Configurar pipeline de CI con los tres pasos obligatorios de la constitución §V
  - **Depende de**: T-001, T-002
  - **Criterios de aceptación**:
    - Archivo `.github/workflows/ci.yml` (o `.azdo/ci.yaml`) creado en la raíz del repositorio
    - Pipeline se activa en todo PR hacia `main` y en push a la rama `001-document-upload-management`
    - Paso 1: `dotnet build` — falla el pipeline si hay errores de compilación
    - Paso 2: `dotnet test --collect:"XPlat Code Coverage"` — cobertura < 80 % BLOQUEA el merge (Coverlet threshold)
    - Paso 3: `dotnet format --verify-no-changes` — falla si hay violaciones de estilo
    - Badge de estado del pipeline referenciado en `README.md`

---

## Fase 2 — Capa de Dominio y Datos

> Entidades C#, enums, Fluent API, migración EF Core e interfaces de repositorio (Application layer).
> Esta fase NO toca lógica de negocio — solo contratos y modelo de datos.

---

- [X] T-008: Crear enums `DocumentCategory` y `DocumentAuditEventType` en `ContosoDashboard/Models/DocumentCategory.cs`
  - **Depende de**: —
  - **Criterios de aceptación**:
    - `DocumentCategory`: `ProjectDocuments=1, TeamResources=2, PersonalFiles=3, Reports=4, Presentations=5, Other=6`
    - `DocumentAuditEventType`: `Uploaded=1, Deleted=2, Replaced=3, ShareGranted=4`
    - Valores `int` explícitos en ambos enums

- [X] T-009 [P]: Crear entidad `Document` en `ContosoDashboard/Models/Document.cs` según `data-model.md §1.1`
  - **Depende de**: T-008
  - **Criterios de aceptación**:
    - Propiedades: `Guid Id`, `string Title`, `string? Description`, `DocumentCategory Category`, `string StoredPath`, `string OriginalFileName`, `string MimeType`, `long FileSizeBytes`, `DateTimeOffset UploadedAtUtc`, `Guid UploadedByUserId`, `User UploadedByUser`, `Guid? ProjectId`, `Project? Project`, `Guid? TaskId`, `ICollection<DocumentTag> Tags`, `ICollection<DocumentShare> Shares`, `ICollection<DocumentAuditLog> AuditLogs`
    - Colecciones inicializadas con `new List<T>()`
    - `sealed class`, nullable reference types habilitados
    - Sin propiedad `IsDeleted` ni ningún marcador de soft-delete — hard-delete únicamente (I1)

- [X] T-010 [P]: Crear entidad `DocumentTag` en `ContosoDashboard/Models/DocumentTag.cs` según `data-model.md §1.2`
  - **Depende de**: T-009
  - **Criterios de aceptación**:
    - Propiedades: `Guid Id`, `Guid DocumentId`, `Document Document`, `string Value`
    - `sealed class`, `Document` inicializado con `default!`

- [X] T-011 [P]: Crear entidad `DocumentShare` en `ContosoDashboard/Models/DocumentShare.cs` según `data-model.md §1.3`
  - **Depende de**: T-009
  - **Criterios de aceptación**:
    - Propiedades: `Guid Id`, `Guid DocumentId`, `Document Document`, `Guid? RecipientUserId`, `User? RecipientUser`, `Guid? RecipientTeamId`, `Guid GrantedByUserId`, `User GrantedByUser`, `DateTimeOffset GrantedAtUtc`

- [X] T-012 [P]: Crear entidad `DocumentAuditLog` en `ContosoDashboard/Models/DocumentAuditLog.cs` según `data-model.md §1.4`
  - **Depende de**: T-008, T-009
  - **Criterios de aceptación**:
    - Propiedades: `Guid Id`, `Guid DocumentId`, `Document Document`, `DocumentAuditEventType EventType`, `Guid ActorUserId`, `User ActorUser`, `DateTimeOffset OccurredAtUtc`
    - `sealed class` — sin setters públicos post-construcción (entidad inmutable)

- [X] T-013: Registrar los cuatro nuevos `DbSet<T>` en `ContosoDashboard/Data/ApplicationDbContext.cs` y añadir la configuración Fluent API completa de `data-model.md §3`
  - **Depende de**: T-009, T-010, T-011, T-012
  - **Criterios de aceptación**:
    - Índices: `(UploadedByUserId, UploadedAtUtc)`, `(ProjectId, UploadedAtUtc)`, `Category` en `Document`
    - Índice único `(DocumentId, Value)` en `DocumentTag`
    - Índice `(DocumentId, RecipientUserId)` en `DocumentShare`
    - Índices `(DocumentId, OccurredAtUtc)` y `(ActorUserId, OccurredAtUtc)` en `DocumentAuditLog`
    - FK `Document.UploadedByUserId` → `OnDelete(Restrict)`, `Document.ProjectId` → `OnDelete(SetNull)`
    - FK `DocumentTag.DocumentId`, `DocumentShare.DocumentId`, `DocumentAuditLog.DocumentId` → `OnDelete(Cascade)`
    - Longitudes: `Title(200)`, `Description(2000)`, `StoredPath(500)`, `OriginalFileName(255)`, `MimeType(255)`, `Tag.Value(100)`

- [X] T-014: Crear y aplicar migración EF Core `AddDocumentManagement` (`dotnet ef migrations add AddDocumentManagement`) y verificar el SQL generado
  - **Depende de**: T-013
  - **Criterios de aceptación**:
    - Archivo `Migrations/..._AddDocumentManagement.cs` generado sin errores
    - `dotnet ef database update` completa sin excepciones en la base de desarrollo
    - Tablas `Documents`, `DocumentTags`, `DocumentShares`, `DocumentAuditLogs` presentes en la BD
    - Ninguna columna `IsDeleted` en ninguna tabla

- [X] T-015 [P]: Definir interfaz `IFileStorageService` con `Task UploadAsync(Stream stream, string path, CancellationToken ct)`, `Task DeleteAsync(string path, CancellationToken ct)`, `Task<Stream> DownloadAsync(string path, CancellationToken ct)`, `Task<string> GetUrlAsync(string path, CancellationToken ct)` en `ContosoDashboard/Services/IFileStorageService.cs`
  - **Depende de**: —
  - **Criterios de aceptación**:
    - Interfaz en namespace `ContosoDashboard.Services` (Application layer)
    - Todos los métodos async-safe (`Task` / `Task<T>`)
    - Sin referencias a `System.IO` concreto en la interfaz

- [X] T-016 [P]: Definir interfaz `IAntivirusScanner` con `Task<ScanResult> ScanAsync(Stream stream, CancellationToken ct)` y `record ScanResult(bool IsClean, string? ThreatName)` en `ContosoDashboard/Services/IAntivirusScanner.cs`
  - **Depende de**: —
  - **Criterios de aceptación**:
    - `ScanResult` es un `record` inmutable
    - Si `IsClean == false`, `ThreatName` nunca es null
    - Sin referencias a ningún proveedor de AV concreto

- [X] T-017 [P]: Implementar `MagicBytesValidator` como clase `static` en `ContosoDashboard/Services/MagicBytesValidator.cs` con whitelist de 6 tipos: PDF, DOCX, XLSX, PPTX, JPEG, PNG
  - **Depende de**: —
  - **Criterios de aceptación**:
    - Método `static bool IsPermitted(ReadOnlySpan<byte> header, out string mimeType)` que inspecciona los primeros bytes
    - 6 magic-byte patterns exactos (FR-001) — TXT excluido: no figura en constitución §IV
    - Retorna `false` para tipos desconocidos — no lanza excepciones
    - Validación basada en bytes, NO en extensión de archivo

- [X] T-018 [P]: Definir interfaz `IDocumentRepository` en `ContosoDashboard/Services/IDocumentRepository.cs` — (C2)
  - **Depende de**: T-009
  - **Criterios de aceptación**:
    - Métodos: `GetByIdAsync(Guid id, CancellationToken ct)`, `GetPagedAsync(DocumentFilter filter, CancellationToken ct)`, `AddAsync(Document doc, CancellationToken ct)`, `UpdateAsync(Document doc, CancellationToken ct)`, `RemoveAsync(Document doc, CancellationToken ct)`, `ExistsAsync(Guid id, CancellationToken ct)`
    - `DocumentFilter` record: `Guid? UserId`, `Guid? ProjectId`, `DocumentCategory? Category`, `DateTimeOffset? FromUtc`, `DateTimeOffset? ToUtc`, `string? SortBy`, `bool SortDescending`, `int Page`, `int PageSize`
    - `PagedResult<T>` record: `IReadOnlyList<T> Items`, `int TotalCount`, `int Page`, `int PageSize`
    - Sin referencias a `DbContext` o `IQueryable` en la interfaz

- [X] T-019 [P]: Definir interfaz `IDocumentAuditLogRepository` en `ContosoDashboard/Services/IDocumentAuditLogRepository.cs` — (C2)
  - **Depende de**: T-012, T-018
  - **Criterios de aceptación**:
    - Métodos: `AddAsync(DocumentAuditLog entry, CancellationToken ct)`, `GetByDocumentIdAsync(Guid documentId, int page, int pageSize, CancellationToken ct)`
    - Sin método de borrado — las entradas de auditoría son inmutables (FR-030)

---

## Fase 3 — Capa de Persistencia y Repositorio

> Implementaciones concretas (Infrastructure layer): File System, AV mock, repositorios EF Core, registro en DI.

---

- [X] T-020: Implementar `LocalFileStorageService : IFileStorageService` en `ContosoDashboard/Data/LocalFileStorageService.cs` usando streaming con `System.IO.FileStream`; ruta base desde `IConfiguration["Storage:BasePath"]` con fallback a `AppData/uploads`
  - **Depende de**: T-015
  - **Criterios de aceptación**:
    - `UploadAsync`: escribe el stream al path; NUNCA carga el stream en `byte[]`
    - `DeleteAsync`: elimina el archivo; si no existe, log `Warning` y retorna sin excepción
    - `DownloadAsync`: retorna `FileStream` en modo `Read` — NUNCA `MemoryStream` del archivo completo
    - `GeneratePath(Guid userId, Guid? projectId, string ext)` genera rutas `{basePath}/{userId}/{projectId-or-personal}/{guid}.{ext}`

- [X] T-021 [P]: Implementar `NoOpAntivirusScanner : IAntivirusScanner` (siempre `IsClean = true`) en `ContosoDashboard/Data/NoOpAntivirusScanner.cs`; registrar solo cuando `ASPNETCORE_ENVIRONMENT == Development`
  - **Depende de**: T-016
  - **Criterios de aceptación**:
    - Registrado condicionalmente — NO disponible en producción
    - Log `Warning` al iniciar: "NoOpAntivirusScanner activo — no usar en producción"

- [X] T-022: Implementar `DocumentRepository : IDocumentRepository` en `ContosoDashboard/Data/DocumentRepository.cs` — (C2)
  - **Depende de**: T-013, T-018
  - **Criterios de aceptación**:
    - `GetByIdAsync`: carga `Tags` y `Shares` vía `Include`; excluye `AuditLogs` por defecto (no se cargan en browse)
    - `GetPagedAsync`: `AsNoTracking()`, aplica todos los filtros de `DocumentFilter`, `Skip`/`Take`; retorna proyección `DocumentSummary` sin `StoredPath`
    - Soporta los 4 valores de `SortBy`: `title`, `uploadDate`, `category`, `fileSize` (FR-010)
    - `AddAsync` / `UpdateAsync` / `RemoveAsync`: operan sobre el `DbContext` sin llamar `SaveChangesAsync` — el handler controla el commit

- [X] T-023 [P]: Implementar `DocumentAuditLogRepository : IDocumentAuditLogRepository` en `ContosoDashboard/Data/DocumentAuditLogRepository.cs` — (C2)
  - **Depende de**: T-013, T-019
  - **Criterios de aceptación**:
    - `AddAsync`: agrega al `DbContext` sin `SaveChangesAsync`
    - `GetByDocumentIdAsync`: `AsNoTracking()`, filtrado por `DocumentId`, orden `OccurredAtUtc DESC`, paginado
    - Sin método de borrado

- [X] T-024: Registrar todos los servicios en DI en `ContosoDashboard/Program.cs`
  - **Depende de**: T-005, T-020, T-022, T-023
  - **Criterios de aceptación**:
    - `IFileStorageService → LocalFileStorageService` (`AddScoped`)
    - `IDocumentRepository → DocumentRepository` (`AddScoped`)
    - `IDocumentAuditLogRepository → DocumentAuditLogRepository` (`AddScoped`)
    - `DocumentAccessService` registrado como `Scoped`
    - `IAntivirusScanner` registrado condicionalmente (Development: `NoOpAntivirusScanner`)
    - `dotnet build` sin errores de resolución de DI

---

## Fase 4 — Capa de Aplicación: CQRS con MediatR

> Commands (escritura) y Queries (lectura) + sus Handlers. Toda la lógica de negocio vive aquí.
> Las páginas Blazor y el controller despachan ÚNICAMENTE vía `IMediator.Send()`. — (C1)

---

### 4.0 — DTOs compartidos

- [X] T-025: Crear `DocumentSummary` y `UploadDocumentResult` en `ContosoDashboard/Services/Documents/DocumentDtos.cs`
  - **Depende de**: T-008, T-009
  - **Criterios de aceptación**:
    - `DocumentSummary` record: `Guid Id`, `string Title`, `string? Description`, `DocumentCategory Category`, `DateTimeOffset UploadedAtUtc`, `Guid UploadedByUserId`, `string UploaderName`, `Guid? ProjectId`, `string? ProjectName`, `long FileSizeBytes`, `string MimeType`, `IReadOnlyList<string> Tags`
    - `UploadDocumentResult` record: `Guid DocumentId`, `string Title`, `DateTimeOffset UploadedAtUtc`
    - `StoredPath` NO expuesto en ningún DTO de Presentation

### 4.1 — Comando: Upload

- [X] T-026: Definir `UploadDocumentCommand` record en `ContosoDashboard/Services/Documents/Commands/UploadDocumentCommand.cs`
  - **Depende de**: T-025
  - **Criterios de aceptación**:
    - `record` que implementa `IRequest<UploadDocumentResult>`
    - Propiedades: `Stream FileStream`, `string OriginalFileName`, `string Title`, `string? Description`, `DocumentCategory Category`, `Guid? ProjectId`, `Guid? TaskId`, `IReadOnlyList<string> Tags`, `Guid ActorUserId`
    - `FileStream` es stream raw — nunca `byte[]`

- [X] T-027: Implementar `UploadDocumentCommandHandler : IRequestHandler<UploadDocumentCommand, UploadDocumentResult>` en `ContosoDashboard/Services/Documents/Commands/UploadDocumentCommandHandler.cs` — pipeline fail-closed de 8 pasos
  - **Depende de**: T-026, T-017, T-016, T-015, T-018, T-019, T-024
  - **Criterios de aceptación**:
    - **Paso 1** — validar `FileStream.Length ≤ 25 MB`; si no, lanzar `DocumentUploadException("FileTooLarge")`
    - **Paso 2** — leer magic-bytes con `MagicBytesValidator.IsPermitted`; si falla, lanzar `DocumentUploadException("InvalidMimeType")`; validación basada en bytes, no en extensión
    - **Paso 3** — llamar `IAntivirusScanner.ScanAsync`; si `!IsClean`, log `Critical` y lanzar `DocumentUploadException("InfectedFile")`; si el scanner no responde, lanzar `DocumentUploadException("ScannerUnavailable")` — fail-closed
    - **Paso 4** — generar ruta GUID con `IFileStorageService`
    - **Paso 5** — `IFileStorageService.UploadAsync(stream, path, ct)`
    - **Paso 6** — `IDocumentRepository.AddAsync(doc, ct)` (sin `SaveChangesAsync` aún)
    - **Paso 7** — `IDocumentAuditLogRepository.AddAsync(new DocumentAuditLog { EventType = Uploaded }, ct)`
    - **Paso 8** — `await dbContext.SaveChangesAsync(ct)` (transacción única pasos 6-7)
    - **Compensación**: si `SaveChangesAsync` falla tras el paso 5, llamar `IFileStorageService.DeleteAsync(path)` y log `Error`; nunca dejar archivos huérfanos en FS
    - Si `TaskId != null` y `ProjectId == null`, resolver `ProjectId` desde `TaskItem` asociado
    - Si `ProjectId != null`, llamar `NotificationService.NotifyProjectDocumentAddedAsync(projectId, documentId, actorUserId, ct)` en bloque `try/catch` aislado **tras** `SaveChangesAsync` exitoso; si falla → log `Warning`; operación NO hace rollback (best-effort FR-024)

### 4.2 — Comando: Eliminar (Hard-Delete)

- [X] T-028: Implementar `DeleteDocumentCommand` record + `DeleteDocumentCommandHandler` en `ContosoDashboard/Services/Documents/Commands/DeleteDocumentCommandHandler.cs`
  - **Depende de**: T-018, T-019, T-015, T-024
  - **Criterios de aceptación**:
    - Command: `Guid DocumentId`, `Guid ActorUserId`
    - RBAC: Owner ✅ | Team Lead del proyecto ✅ | Project Manager ✅ | cualquier otro → `ForbiddenException`
    - **Orden obligatorio**: (1) `IFileStorageService.DeleteAsync` → (2) `IDocumentRepository.RemoveAsync` → (3) `IDocumentAuditLogRepository.AddAsync(Deleted)` → (4) `SaveChangesAsync`
    - Si `SaveChangesAsync` falla después de (1), log `Error` con ruta del archivo perdido — la excepción se propaga
    - Hard-delete únicamente — ningún flag `IsDeleted`, ningún soft-delete (I1)
    - Tags, Shares y AuditLogs anteriores eliminados por Cascade (FK Fluent API)

### 4.3 — Comando: Actualizar Metadatos

- [X] T-029: Implementar `UpdateDocumentMetadataCommand` record + `UpdateDocumentMetadataCommandHandler` en `ContosoDashboard/Services/Documents/Commands/UpdateDocumentMetadataCommandHandler.cs`
  - **Depende de**: T-018, T-024
  - **Criterios de aceptación**:
    - Command: `Guid DocumentId`, `string Title`, `string? Description`, `DocumentCategory Category`, `IReadOnlyList<string> Tags`, `Guid ActorUserId`
    - RBAC: Owner ✅ | Team Lead del proyecto ✅ | Project Manager ✅ | otro → `ForbiddenException`
    - Tags: reemplazar colección completa dentro de la misma transacción
    - Sin entrada de `DocumentAuditLog` (solo eventos write de archivo generan audit)
    - `SaveChangesAsync` una sola llamada al final

### 4.4 — Comando: Reemplazar Archivo

- [X] T-030: Implementar `ReplaceDocumentCommand` record + `ReplaceDocumentCommandHandler` en `ContosoDashboard/Services/Documents/Commands/ReplaceDocumentCommandHandler.cs`
  - **Depende de**: T-027, T-028
  - **Criterios de aceptación**:
    - RBAC idéntico a Delete
    - Reutiliza el pipeline completo de upload (magic-bytes, AV scan, GUID, store) para el nuevo archivo
    - Si nuevo archivo se guarda Y `SaveChangesAsync` tiene éxito → eliminar archivo antiguo
    - Si `SaveChangesAsync` falla → eliminar nuevo archivo (compensación), archivo antiguo intacto
    - Audit log: `EventType = Replaced`
    - Actualiza `StoredPath`, `MimeType`, `FileSizeBytes`, `OriginalFileName` en la entidad

### 4.5 — Comando: Compartir

- [X] T-031: Implementar `ShareDocumentCommand` record + `ShareDocumentCommandHandler` en `ContosoDashboard/Services/Documents/Commands/ShareDocumentCommandHandler.cs`
  - **Depende de**: T-018, T-019, T-024
  - **Criterios de aceptación**:
    - Command: `Guid DocumentId`, `Guid ActorUserId`, `IReadOnlyList<Guid> RecipientUserIds`, `IReadOnlyList<Guid> RecipientTeamIds`
    - **A1**: `RecipientTeamIds` contiene `ProjectId` — "equipo" = todos los `ProjectMember.UserId` activos del proyecto; no existe entidad `Team` independiente en v1
    - RBAC: solo Owner → cualquier otro → `ForbiddenException`
    - Para cada `RecipientTeamId`, resolver miembros con `ProjectMember.ProjectId == teamId` y crear un `DocumentShare` por miembro activo
    - Crea `DocumentShare` por cada recipient (directo o miembro de proyecto) + `DocumentAuditLog { ShareGranted }` por cada share en la misma transacción
    - `SaveChangesAsync` único que incluye shares + audit logs
    - **G3**: Extender `ContosoDashboard/Services/NotificationService.cs` con `Task NotifyShareAsync(Guid documentId, IReadOnlyList<Guid> recipientUserIds, CancellationToken ct)` — crea una `Notification` por destinatario
    - Notificaciones vía `NotificationService.NotifyShareAsync(documentId, allRecipientUserIds, ct)` en bloque `try/catch` aislado — si falla, log `Warning`; la operación NO hace rollback (best-effort FR-024)

### 4.6 — Queries de Lectura

- [X] T-032 [P]: Definir `GetMyDocumentsQuery` record + implementar `GetMyDocumentsQueryHandler` en `ContosoDashboard/Services/Documents/Queries/GetMyDocumentsQueryHandler.cs`
  - **Depende de**: T-018, T-025
  - **Criterios de aceptación**:
    - Query: `Guid UserId`, `DocumentFilter Filter`
    - Soporta sort por `title`, `uploadDate`, `category`, `fileSize` (4 campos — FR-010)
    - Retorna `PagedResult<DocumentSummary>` con `AsNoTracking()`
    - `AuditLogs` NO cargados en esta query

- [X] T-033 [P]: Definir `GetProjectDocumentsQuery` record + `GetProjectDocumentsQueryHandler` en `ContosoDashboard/Services/Documents/Queries/GetProjectDocumentsQueryHandler.cs`
  - **Depende de**: T-018, T-025
  - **Criterios de aceptación**:
    - RBAC: verificar que `ActorUserId` es miembro del proyecto — si no → `ForbiddenException`
    - `AsNoTracking()` + paginación

- [X] T-034 [P]: Definir `SearchDocumentsQuery` record + `SearchDocumentsQueryHandler` en `ContosoDashboard/Services/Documents/Queries/SearchDocumentsQueryHandler.cs`
  - **Depende de**: T-018, T-025
  - **Criterios de aceptación**:
    - Búsqueda en `Title`, `Description`, `Tags.Value`, `UploadedByUser.Name`, `Project.Name`
    - Resultados filtrados por RBAC (own / project member / shared) — FR-013
    - `AsNoTracking()` + paginación

- [X] T-035 [P]: Definir `GetSharedWithMeQuery` record + `GetSharedWithMeQueryHandler` en `ContosoDashboard/Services/Documents/Queries/GetSharedWithMeQueryHandler.cs`
  - **Depende de**: T-018, T-025
  - **Criterios de aceptación**:
    - Retorna docs donde `RecipientUserId == actorUserId` o el usuario pertenece al equipo en `RecipientTeamId`
    - Solo `DocumentSummary` — sin `StoredPath`

- [X] T-036 [P]: Definir `GetRecentDocumentsQuery` + `GetDocumentCountQuery` y sus handlers en `ContosoDashboard/Services/Documents/Queries/DashboardQueriesHandlers.cs`
  - **Depende de**: T-018, T-025
  - **Criterios de aceptación**:
    - `GetRecentDocumentsQuery`: últimos 5 documentos del usuario por `UploadedAtUtc DESC` — FR-022
    - `GetDocumentCountQuery`: total de documentos accesibles para el usuario — FR-023
    - Ambas `AsNoTracking()`

- [X] T-037 [P]: Definir `GetDocumentAuditLogQuery` record + `GetDocumentAuditLogQueryHandler` en `ContosoDashboard/Services/Documents/Queries/GetDocumentAuditLogQueryHandler.cs`
  - **Depende de**: T-019, T-025
  - **Criterios de aceptación**:
    - RBAC: solo rol `Administrator` — cualquier otro → `ForbiddenException` (FR-029)
    - Retorna `PagedResult<DocumentAuditLog>` ordenado por `OccurredAtUtc DESC`

### 4.7 — Servicio Auxiliar: Autorización de Acceso

- [X] T-038: Implementar `DocumentAccessService` en `ContosoDashboard/Services/DocumentAccessService.cs` con `Task AuthorizeAccessAsync(Guid documentId, Guid actorUserId, CancellationToken ct)`
  - **Depende de**: T-018
  - **Criterios de aceptación**:
    - Acceso permitido: Owner ✅ | Project member (docs de proyecto) ✅ | Recipient en `DocumentShare` ✅ | Administrator ✅
    - Lanza `DocumentNotFoundException` si el documento no existe
    - Lanza `ForbiddenException` si ninguna condición se cumple
    - Usado internamente por los handlers de Download y Preview

---

## Fase 5 — Capa de Presentación (UI Blazor + Controller)

> Controller REST y páginas Blazor. Toda invocación pasa por `IMediator.Send()`. — (C1)

---

- [X] T-039: Implementar `DocumentsController` en `ContosoDashboard/Controllers/DocumentsController.cs` con todos los endpoints del contrato `document-management-api.yaml`
  - **Depende de**: T-027, T-028, T-029, T-030, T-031, T-032, T-034, T-035, T-037, T-038
  - **Criterios de aceptación**:
    - `POST /api/documents/upload` → `UploadDocumentCommand` → 201 + `DocumentSummary`
    - `GET /api/documents` → `GetMyDocumentsQuery` → `PagedResult<DocumentSummary>`
    - `GET /api/documents/search` → `SearchDocumentsQuery`
    - `GET /api/documents/{id}/download` → autoriza con `DocumentAccessService` → stream `FileStreamResult` — NO `byte[]`
    - `GET /api/documents/{id}/preview` → `Content-Disposition: inline`; solo PDF/JPEG/PNG — resto retorna 400
    - `PATCH /api/documents/{id}/metadata` → `UpdateDocumentMetadataCommand`
    - `POST /api/documents/{id}/replace` → `ReplaceDocumentCommand`
    - `DELETE /api/documents/{id}` → `DeleteDocumentCommand` → 204
    - `POST /api/documents/{id}/share` → `ShareDocumentCommand`
    - `GET /api/documents/{id}/audit` → `GetDocumentAuditLogQuery` (Admin only)
    - `[Authorize]` en todos los endpoints

- [X] T-040: Crear página Blazor `ContosoDashboard/Pages/DocumentUpload.razor`
  - **Depende de**: T-027, T-005
  - **Criterios de aceptación**:
    - Campos: `IBrowserFile` input, `Title` (requerido), `Category` (requerido), `Description` (opcional), `ProjectId` (opcional), tags (opcional)
    - Barra de progreso durante la transferencia del stream
    - Stream de `IBrowserFile` se pasa directamente al command — NO se carga en memoria completa
    - Mensajes de error para: archivo muy grande, tipo no permitido, AV falla, error genérico
    - Mensaje de éxito con enlace a "Mis Documentos"

- [X] T-041: Crear página Blazor `ContosoDashboard/Pages/MyDocuments.razor`
  - **Depende de**: T-032
  - **Criterios de aceptación**:
    - Tabla paginada: Título, Categoría, Fecha de subida, Tamaño, Proyecto
    - Headers clickeables para sort por: título, fecha, categoría, tamaño (FR-010)
    - Filtros: categoría, proyecto, rango de fechas
    - Paginación server-side, 20 default, max 100
    - Modal de confirmación antes de ejecutar "Eliminar"
    - Dispatcha vía `IMediator.Send()`

- [X] T-042: Agregar tab "Documentos" a `ContosoDashboard/Pages/ProjectDetails.razor` y shortcut de upload con `projectId` pre-cargado
  - **Depende de**: T-033, T-040
  - **Criterios de aceptación**:
    - Tab visible solo para miembros del proyecto
    - Botón "Subir al proyecto" pre-llena `ProjectId` en el formulario de upload
    - RBAC verificado en el handler — la UI solo oculta controles

- [X] T-043: Crear página Blazor `ContosoDashboard/Pages/SharedWithMe.razor`
  - **Depende de**: T-035
  - **Criterios de aceptación**:
    - Lista paginada: Título, Compartido por, Fecha, Categoría
    - Solo acciones de lectura y descarga — sin editar ni eliminar
    - Nav link "Compartido conmigo" en `Shared/NavMenu.razor`

- [X] T-044: Crear página Blazor `ContosoDashboard/Pages/DocumentSearch.razor`
  - **Depende de**: T-034
  - **Criterios de aceptación**:
    - Barra de búsqueda con submit por Enter
    - Resultados paginados con mismos controles que My Documents
    - Nav link "Buscar documentos" en `Shared/NavMenu.razor`

- [X] T-045: Actualizar `ContosoDashboard/Pages/Index.razor` — widget de documentos recientes y card de conteo
  - **Depende de**: T-036
  - **Criterios de aceptación**:
    - `<RecentDocumentsWidget />` integrado — últimos 5 documentos (FR-022)
    - Card "Documentos" con total accesible (FR-023)

- [X] T-046: Crear componente Blazor `ContosoDashboard/Shared/RecentDocumentsWidget.razor`
  - **Depende de**: T-036
  - **Criterios de aceptación**:
    - Máximo 5 filas: Título (link), Fecha de subida
    - Si 0 documentos, mensaje "Aún no has subido documentos"
    - Recibe resultado de la query por parámetro — no hace fetch propio

- [X] T-047: Actualizar `ContosoDashboard/Pages/Tasks.razor` — panel de documentos adjuntos y upload desde tarea
  - **Depende de**: T-032, T-027
  - **Criterios de aceptación**:
    - Panel colapsable "Documentos adjuntos" en detalle de tarea con botón de descarga por item
    - Botón "Adjuntar documento" pre-llena `taskId` y `projectId`
    - `ProjectId` resuelto automáticamente desde la tarea si no es provisto

- [X] T-048: Crear página Blazor `ContosoDashboard/Pages/DocumentAuditLog.razor` (solo Admin)
  - **Depende de**: T-037
  - **Criterios de aceptación**:
    - Solo accesible con rol `Administrator`; redirect a 403 para otros roles
    - Tabla paginada: Tipo de evento, Actor, Fecha UTC
    - Nav link visible solo para Admins en `Shared/NavMenu.razor`

---

## Fase 6 — Pruebas Automatizadas (QA)

> Cobertura mínima ≥ 80 % en Application + Domain (CI gate).
> Incluye las 5 pruebas adicionales identificadas en auditoría: G1 (3 sorts) + G2 (2 RBAC Team Lead).

---

### 6.1 — Tests Unitarios

- [X] T-049: Tests para `MagicBytesValidator` en `ContosoDashboard.Tests.Unit/MagicBytesValidatorTests.cs`
  - **Depende de**: T-017
  - **Criterios de aceptación**:
    - `IsPermitted_ValidPdf_ReturnsTrue`, `_ValidDocx_`, `_ValidXlsx_`, `_ValidPptx_`, `_ValidJpeg_`, `_ValidPng_` (6 tests — TXT excluido per constitución §IV)
    - `IsPermitted_ExecutableBytes_ReturnsFalse`
    - `IsPermitted_EmptyBytes_ReturnsFalse`
    - `IsPermitted_TruncatedHeader_ReturnsFalse`

- [X] T-050: Tests para `UploadDocumentCommandHandler` en `ContosoDashboard.Tests.Unit/UploadDocumentCommandHandlerTests.cs`
  - **Depende de**: T-027
  - **Criterios de aceptación**:
    - `Handle_ValidPdf_PersistsDocumentAndAuditLog`
    - `Handle_FileTooLarge_ThrowsDocumentUploadException`
    - `Handle_InvalidMagicBytes_ThrowsDocumentUploadException_NoFileStored`
    - `Handle_ScannerFlagsInfected_ThrowsDocumentUploadException_LogsCritical`
    - `Handle_ScannerUnavailable_ThrowsDocumentUploadException` — fail-closed
    - `Handle_DbSaveFailsAfterFileStored_DeletesFileAndThrows` — compensación FS

- [X] T-051: Tests para `DeleteDocumentCommandHandler` en `ContosoDashboard.Tests.Unit/DeleteDocumentCommandHandlerTests.cs`
  - **Depende de**: T-028
  - **Criterios de aceptación**:
    - `Handle_Owner_HardDeletesFileAndRow` — orden: `DeleteAsync(FS)` → `RemoveAsync(repo)` → `AuditLog(Deleted)` → `SaveChangesAsync`; sin `IsDeleted` flag
    - `Handle_ProjectManager_Succeeds`
    - `Handle_TeamLeadInProject_Succeeds` — **G2a: remediación de auditoría**
    - `Handle_EmployeeOtherOwner_ThrowsForbiddenException`
    - `Handle_DocumentNotFound_ThrowsDocumentNotFoundException`

- [X] T-052: Tests para `GetMyDocumentsQueryHandler` en `ContosoDashboard.Tests.Unit/GetMyDocumentsQueryHandlerTests.cs`
  - **Depende de**: T-032
  - **Criterios de aceptación**:
    - `Handle_ReturnsOnlyUploadersDocuments_Paginated`
    - `Handle_FilterByCategory_ReturnsMatchingOnly`
    - `Handle_FilterByDateRange_ReturnsMatchingOnly`
    - `Handle_SortByUploadDateDesc_OrderCorrect`
    - `Handle_SortByTitle_OrderCorrect` — **G1a: remediación de auditoría**
    - `Handle_SortByCategory_OrderCorrect` — **G1b: remediación de auditoría**
    - `Handle_SortByFileSize_OrderCorrect` — **G1c: remediación de auditoría**

- [X] T-053 [P]: Tests para `UpdateDocumentMetadataCommandHandler` en `ContosoDashboard.Tests.Unit/UpdateDocumentMetadataCommandHandlerTests.cs`
  - **Depende de**: T-029
  - **Criterios de aceptación**:
    - `Handle_Owner_UpdatesMetadataAndReplacesTagsInTransaction`
    - `Handle_TeamLeadInProject_Succeeds` — **G2b: remediación de auditoría**
    - `Handle_ProjectManager_Succeeds`
    - `Handle_NonOwnerEmployee_ThrowsForbiddenException`

- [X] T-054 [P]: Tests para `ShareDocumentCommandHandler` en `ContosoDashboard.Tests.Unit/ShareDocumentCommandHandlerTests.cs`
  - **Depende de**: T-031
  - **Criterios de aceptación**:
    - `Handle_Owner_CreatesShareRecordsAndAuditLogs`
    - `Handle_NonOwner_ThrowsForbiddenException`
    - `Handle_NotificationFails_OperationSucceeds_WarningLogged` — best-effort FR-024

- [X] T-055 [P]: Tests para `SearchDocumentsQueryHandler` en `ContosoDashboard.Tests.Unit/SearchDocumentsQueryHandlerTests.cs`
  - **Depende de**: T-034
  - **Criterios de aceptación**:
    - `Handle_SearchByTitle_ReturnsMatchingDocuments`
    - `Handle_SearchByTag_ReturnsMatchingDocuments`
    - `Handle_ExcludesDocumentsOutsideUserRbacScope`
    - `Handle_SearchByProject_ReturnsMatchingDocuments`

- [X] T-056 [P]: Tests para `ReplaceDocumentCommandHandler` en `ContosoDashboard.Tests.Unit/ReplaceDocumentCommandHandlerTests.cs`
  - **Depende de**: T-030
  - **Criterios de aceptación**:
    - `Handle_Owner_ReplacesFileAndUpdatesMetadata`
    - `Handle_DbSaveFailsAfterNewFileSaved_DeletesNewFileAndLeavesOldIntact`

- [X] T-057 [P]: Tests para `DocumentAccessService` en `ContosoDashboard.Tests.Unit/DocumentAccessServiceTests.cs`
  - **Depende de**: T-038
  - **Criterios de aceptación**:
    - `AuthorizeAccessAsync_Owner_Granted`
    - `AuthorizeAccessAsync_ProjectMember_Granted`
    - `AuthorizeAccessAsync_SharedRecipient_Granted`
    - `AuthorizeAccessAsync_Administrator_Granted`
    - `AuthorizeAccessAsync_NoAccess_ThrowsForbiddenException`
    - `AuthorizeAccessAsync_DocumentNotFound_ThrowsDocumentNotFoundException`

### 6.2 — Tests de Integración

- [X] T-058: Tests end-to-end del pipeline de upload en `ContosoDashboard.Tests.Integration/DocumentUploadIntegrationTests.cs` (WebApplicationFactory + Testcontainers.SqlServer)
  - **Depende de**: T-039, T-003
  - **Criterios de aceptación**:
    - `Upload_ValidPdf_Returns201_AndPersistsBothDbRowAndFile`
    - `Upload_OversizedFile_Returns413`
    - `Upload_UnsupportedMime_Returns400`
    - `Upload_InfectedFile_Returns400OrServiceUnavailable` (MockInfectedScanner)
    - `Upload_Unauthenticated_Returns401`

- [X] T-059 [P]: Tests de acceso en `ContosoDashboard.Tests.Integration/DocumentAccessIntegrationTests.cs`
  - **Depende de**: T-039, T-003
  - **Criterios de aceptación**:
    - `Download_AuthorizedUser_StreamsFileWithCorrectMime`
    - `Download_UnauthorizedUser_Returns403`
    - `Preview_PdfFile_ReturnsInlineContentDisposition`
    - `Preview_UnsupportedType_Returns400`

- [X] T-060 [P]: Tests de hard-delete en `ContosoDashboard.Tests.Integration/DocumentHardDeleteIntegrationTests.cs`
  - **Depende de**: T-039, T-003
  - **Criterios de aceptación**:
    - `Delete_Owner_Returns204_RemovesBothDbRowAndFile` — verifica ausencia de fila en BD Y ausencia de archivo en FS
    - `Delete_Unauthorized_Returns403`
    - `Delete_WritesAuditLogEntry`
    - Ninguna columna `IsDeleted` en la BD (verificar por ausencia completa de fila)

- [X] T-061 [P]: Tests del audit log en `ContosoDashboard.Tests.Integration/DocumentAuditLogIntegrationTests.cs`
  - **Depende de**: T-039, T-003
  - **Criterios de aceptación**:
    - Flujo upload → delete → share genera 3 entradas en `DocumentAuditLogs`
    - `GET /api/documents/{id}/audit` retorna entradas correctas para Admin
    - `GET /api/documents/{id}/audit` retorna 403 para rol Employee

- [X] T-062 [P]: Test de rendimiento de búsqueda en `ContosoDashboard.Tests.Integration/DocumentSearchPerformanceTests.cs`
  - **Depende de**: T-039, T-003
  - **Criterios de aceptación**:
    - Seed de 500 documentos con tags variados en la BD de test
    - `SearchDocuments_500Docs_CompletesInUnder2Seconds` — tiempo medido con `Stopwatch` (SC-003)

### 6.3 — Tests de Componente (bUnit)

- [X] T-063 [P]: Tests bUnit para `DocumentUpload.razor` en `ContosoDashboard.Tests.UI/DocumentUploadComponentTests.cs`
  - **Depende de**: T-040, T-004
  - **Criterios de aceptación**:
    - `Render_ShowsRequiredFields_TitleAndCategory`
    - `Submit_WithoutTitle_ShowsValidationError`
    - `Submit_ValidFile_ShowsProgressIndicator`
    - `Submit_OversizedFile_ShowsFileTooLargeError`

- [X] T-064 [P]: Tests bUnit para `MyDocuments.razor` en `ContosoDashboard.Tests.UI/MyDocumentsComponentTests.cs`
  - **Depende de**: T-041, T-004
  - **Criterios de aceptación**:
    - `Render_WithDocuments_ShowsPaginationControls`
    - `ClickSortByTitle_TriggersNewQuery`
    - `FilterByCategory_TriggersNewQuery`
    - `ClickDelete_ShowsConfirmationDialogBeforeDispatching`

- [X] T-065 [P]: Tests bUnit para `RecentDocumentsWidget.razor` en `ContosoDashboard.Tests.UI/RecentDocumentsWidgetTests.cs`
  - **Depende de**: T-046, T-004
  - **Criterios de aceptación**:
    - `Render_With5Documents_ShowsAll5`
    - `Render_WithMoreThan5_ShowsOnly5`
    - `Render_WithNoDocuments_ShowsEmptyStateMessage`

---

## Grafo de Dependencias

```
F1 (T-001 a T-007)
  └─► F2 (T-008 a T-019)
          └─► F3 (T-020 a T-024)
                  └─► F4 (T-025 a T-038) ──────────────────────────────────────────────┐
                          │                                                              │
                          ├─ T-027 (Upload Handler) ◄──── T-026 (Command)              │
                          ├─ T-028 (Delete Handler)                                     │
                          ├─ T-029 (Update Metadata Handler)                            │
                          ├─ T-030 (Replace Handler) ◄─── T-027 + T-028               │
                          ├─ T-031 (Share Handler)                                      │
                          └─ T-032 a T-037 (Queries) [paralelas]                       │
                                  │                                                     │
                                  ▼                                                     │
                          F5 (T-039 a T-048) ◄─────────────────────────────────────────┘
                                  │
                                  ▼
                          F6 (T-049 a T-065)
```

## Ejecución Paralela

| Fase | Tareas paralelizables |
|------|-----------------------|
| F1 | T-002, T-003, T-004 (post T-001) |
| F2 | T-009–T-012, T-015–T-019 (post T-008 para enums) |
| F3 | T-021, T-023 (post sus dependencias) |
| F4 | T-032–T-037 (Queries independientes entre sí) |
| F5 | T-040–T-048 (post sus handlers respectivos) |
| F6 | T-053–T-065 (post sus units respectivos) |

## MVP Mínimo Viable

```
F1 completa (T-001 a T-007)
  + F2 completa (T-008 a T-019)
  + F3 completa (T-020 a T-024)
  + T-025 (DTOs) + T-026 + T-027 (Upload Command + Handler)
  + T-032 (GetMyDocumentsQuery + Handler)
  + T-039 (Controller — endpoints upload + browse)
  + T-040 (Upload page) + T-041 (My Documents page)
  + T-049, T-050 (Unit tests)
  + T-058 (Integration tests upload)
```

MVP entrega: subir un documento → verlo en "Mis Documentos" → descargarlo.
Todo lo demás es iteración incremental sobre esta base.

---

## Métricas del Plan

| Métrica | Valor |
|---------|-------|
| Total de tareas | **67** |
| Tareas paralelizables `[P]` | ~30 |
| Fases lógicas | 6 |
| FRs cubiertos | 30/30 (100 %) |
| Test methods unitarios cubiertos | ≥ 41 |
| Suites de integración | 5 |
| Suites bUnit | 3 |
| Remediaciones speckit.tasks | C1-MediatR ✅ C2-Repository ✅ I1-HardDelete ✅ G1-Sorts ✅ G2-RBAC ✅ |
| Remediaciones speckit.analyze | C1-TXT ✅ I1-FR018 ✅ G1-Serilog ✅ G2-ProjNotif ✅ G3-NotifyShare ✅ G4-CI ✅ A1-TeamDef ✅ |
| Violaciones de constitución pendientes | **0** |
| Estado de la spec | ✅ Green — Listo para `/speckit.implement` |

