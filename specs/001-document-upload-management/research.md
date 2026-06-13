# Research: Document Upload and Management

## Decision 1: Storage Strategy for v1

- **Decision**: Use local File System storage outside web root (`AppData/uploads`) with GUID-based paths, behind `IFileStorageService` abstraction (`UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync`).
- **Rationale**: Meets security requirements (no direct URL access, isolated physical storage), keeps implementation simple for v1, and preserves transparent migration path to Azure Blob Storage.
- **Alternatives considered**:
  - Azure Blob Storage from day 1: rejected for v1 due to higher infrastructure complexity and deployment prerequisites.
  - Database BLOB storage: rejected due to larger DB growth and poorer operational ergonomics for large file streaming.

## Decision 2: Upload Pipeline and Security

- **Decision**: Implement strict sequential pipeline with fail-closed policy:
  1. Receive file as stream
  2. Validate file size (`<=25 MB`) and magic-bytes/MIME whitelist
  3. Run synchronous `IAntivirusScanner` scan
  4. Generate GUID path
  5. Persist file
  6. Persist metadata in DB
- **Rationale**: Ensures no unsafe artifact is ever persisted; aligns with constitution non-negotiables for security and streaming-based I/O.
- **Alternatives considered**:
  - Validate only by extension/content-type header: rejected (spoofable).
  - Async/offline virus scanning: rejected for v1 because file could become temporarily accessible before scan verdict.

## Decision 3: Hard-Delete Behavior

- **Decision**: Hard-delete only. On delete request: remove DB record and physical file in a safe compensating flow; if one step fails, operation reports failure and logs error.
- **Rationale**: Clarified requirement selected hard-delete; no regulatory retention requirement in v1.
- **Alternatives considered**:
  - Soft-delete with retention window: rejected by clarification outcomes.
  - Delete file only and keep metadata: rejected due to inconsistent data state.

## Decision 4: Audit Logging Model

- **Decision**: Add `DocumentAuditLog` entity for write events only (`Upload`, `Delete`, `Replace`, `ShareGranted`).
- **Rationale**: Provides compliance/traceability baseline with minimal overhead and no penalty on browse/search read paths.
- **Alternatives considered**:
  - Full read+write audit trail: rejected for v1 due to higher volume and cost.
  - No audit log: rejected because FR-030 requires immutable write-event tracking.

## Decision 5: Notification Delivery Semantics

- **Decision**: Best-effort notification dispatch; upload/share operations succeed even if notification delivery fails, with `Warning` logs.
- **Rationale**: Keeps primary business transactions reliable and avoids rollback of successful document operations.
- **Alternatives considered**:
  - Transactional notifications with rollback: rejected because messaging reliability should not block document CRUD.
  - Retry queue in v1: deferred; acceptable enhancement for a later iteration.

## Decision 6: Team Lead Authorization Scope

- **Decision**: Team Lead authority is project-scoped only.
- **Rationale**: Matches existing project-centric model and avoids coupling to org-chart logic.
- **Alternatives considered**:
  - Org-chart scoped authority: rejected as out of scope for current domain model.
  - Explicit Team aggregate authority model: deferred to future organization module evolution.
