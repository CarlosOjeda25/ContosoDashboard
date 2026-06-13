# Data Model: Document Upload and Management

## 1. Entity Model (C#)

### 1.1 Document

```csharp
public sealed class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentCategory Category { get; set; }
    public string StoredPath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = default!;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    public ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
    public ICollection<DocumentShare> Shares { get; set; } = new List<DocumentShare>();
    public ICollection<DocumentAuditLog> AuditLogs { get; set; } = new List<DocumentAuditLog>();
}
```

### 1.2 DocumentTag

```csharp
public sealed class DocumentTag
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;
    public string Value { get; set; } = string.Empty;
}
```

### 1.3 DocumentShare

```csharp
public sealed class DocumentShare
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    public Guid? RecipientUserId { get; set; }
    public User? RecipientUser { get; set; }

    public Guid? RecipientTeamId { get; set; }

    public Guid GrantedByUserId { get; set; }
    public User GrantedByUser { get; set; } = default!;

    public DateTimeOffset GrantedAtUtc { get; set; }
}
```

### 1.4 DocumentAuditLog

```csharp
public sealed class DocumentAuditLog
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = default!;

    public DocumentAuditEventType EventType { get; set; }

    public Guid ActorUserId { get; set; }
    public User ActorUser { get; set; } = default!;

    public DateTimeOffset OccurredAtUtc { get; set; }
}
```

### 1.5 Enums

```csharp
public enum DocumentCategory
{
    ProjectDocuments = 1,
    TeamResources = 2,
    PersonalFiles = 3,
    Reports = 4,
    Presentations = 5,
    Other = 6
}

public enum DocumentAuditEventType
{
    Uploaded = 1,
    Deleted = 2,
    Replaced = 3,
    ShareGranted = 4
}
```

## 2. Relationships

- `User (1) -> (many) Document` through `Document.UploadedByUserId`
- `Project (0..1) -> (many) Document` through `Document.ProjectId`
- `Document (1) -> (many) DocumentTag`
- `Document (1) -> (many) DocumentShare`
- `Document (1) -> (many) DocumentAuditLog`
- `User (1) -> (many) DocumentAuditLog` through `ActorUserId`
- `User (1) -> (many) DocumentShare` through `GrantedByUserId`

## 3. Fluent API Configuration (EF Core)

```csharp
modelBuilder.Entity<Document>(entity =>
{
    entity.ToTable("Documents");
    entity.HasKey(x => x.Id);

    entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
    entity.Property(x => x.Description).HasMaxLength(2000);
    entity.Property(x => x.StoredPath).HasMaxLength(500).IsRequired();
    entity.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
    entity.Property(x => x.MimeType).HasMaxLength(255).IsRequired();
    entity.Property(x => x.FileSizeBytes).IsRequired();
    entity.Property(x => x.UploadedAtUtc).IsRequired();

    entity.HasIndex(x => new { x.UploadedByUserId, x.UploadedAtUtc });
    entity.HasIndex(x => new { x.ProjectId, x.UploadedAtUtc });
    entity.HasIndex(x => x.Category);

    entity.HasOne(x => x.UploadedByUser)
        .WithMany()
        .HasForeignKey(x => x.UploadedByUserId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne(x => x.Project)
        .WithMany()
        .HasForeignKey(x => x.ProjectId)
        .OnDelete(DeleteBehavior.SetNull);
});

modelBuilder.Entity<DocumentTag>(entity =>
{
    entity.ToTable("DocumentTags");
    entity.HasKey(x => x.Id);

    entity.Property(x => x.Value).HasMaxLength(100).IsRequired();
    entity.HasIndex(x => new { x.DocumentId, x.Value }).IsUnique();

    entity.HasOne(x => x.Document)
        .WithMany(x => x.Tags)
        .HasForeignKey(x => x.DocumentId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<DocumentShare>(entity =>
{
    entity.ToTable("DocumentShares");
    entity.HasKey(x => x.Id);

    entity.Property(x => x.GrantedAtUtc).IsRequired();
    entity.HasIndex(x => new { x.DocumentId, x.RecipientUserId });

    entity.HasOne(x => x.Document)
        .WithMany(x => x.Shares)
        .HasForeignKey(x => x.DocumentId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(x => x.GrantedByUser)
        .WithMany()
        .HasForeignKey(x => x.GrantedByUserId)
        .OnDelete(DeleteBehavior.Restrict);
});

modelBuilder.Entity<DocumentAuditLog>(entity =>
{
    entity.ToTable("DocumentAuditLogs");
    entity.HasKey(x => x.Id);

    entity.Property(x => x.EventType).IsRequired();
    entity.Property(x => x.OccurredAtUtc).IsRequired();

    entity.HasIndex(x => new { x.DocumentId, x.OccurredAtUtc });
    entity.HasIndex(x => new { x.ActorUserId, x.OccurredAtUtc });

    entity.HasOne(x => x.Document)
        .WithMany(x => x.AuditLogs)
        .HasForeignKey(x => x.DocumentId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(x => x.ActorUser)
        .WithMany()
        .HasForeignKey(x => x.ActorUserId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

## 4. Hard-Delete Transactional Flow

1. Authorize delete at service layer (owner/project manager/team lead per project scope)
2. Load target document metadata + stored path
3. Delete file via `IFileStorageService.DeleteAsync(storedPath)`
4. Delete DB row (`Document`) + dependent (`DocumentTag`, `DocumentShare`) in same unit-of-work
5. Insert audit record (`DocumentAuditLog: Deleted`) in same DB transaction before commit
6. Commit transaction
7. If DB commit fails after file delete, log `Error` and enqueue compensation workflow (operational repair)

## 5. Read-Path Performance Strategy

- Browse/search queries use projection DTOs with `AsNoTracking()`
- `DocumentAuditLog` is excluded from default read projections
- Audit log fetched only on explicit admin endpoint `/api/documents/{id}/audit`
- Pagination mandatory for list/search endpoints
