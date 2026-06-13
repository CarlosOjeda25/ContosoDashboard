# Quickstart: Document Upload and Management

## 1. Pre-requisitos

- .NET SDK 10 instalado
- Base de datos de desarrollo configurada en `appsettings.Development.json`
- Servicio antivirus implementando `IAntivirusScanner` (modo mock para local si aplica)
- Directorio de almacenamiento local creado fuera de web root (ejemplo: `AppData/uploads`)

## 2. Implementacion por pasos (orden recomendado)

1. **Modelo y DbContext**
   - Agregar entidades: `Document`, `DocumentTag`, `DocumentShare`, `DocumentAuditLog`
   - Configurar Fluent API e indices
   - Crear y aplicar migracion EF Core

2. **Abstracciones de infraestructura**
   - Crear interfaces: `IFileStorageService`, `IAntivirusScanner`
   - Implementar `LocalFileStorageService` para File System local

3. **Servicios de aplicacion (capa servicio)**
   - `DocumentUploadService` con pipeline secuencial fail-closed
   - `DocumentQueryService` para browse/search paginado
   - `DocumentAccessService` para download/preview con RBAC
   - `DocumentSharingService` para share + notificacion best-effort

4. **Endpoints/Pages Blazor**
   - Upload con progress indicator
   - My Documents, Project Documents, Shared with Me
   - Preview/download
   - Metadata edit, replace, delete (hard-delete)
   - Admin audit log viewer

5. **Integracion en dashboard/tasks**
   - Widget `Recent Documents` (ultimo 5)
   - Conteo de documentos en cards
   - Asociacion de upload desde task al proyecto padre

## 3. Upload Pipeline de referencia

1. Recibir stream del archivo
2. Validar tamano (`<= 25 MB`)
3. Validar magic-bytes + MIME permitido
4. Escanear con `IAntivirusScanner` (sincrono)
5. Generar ruta GUID
6. Guardar archivo
7. Guardar metadatos DB
8. Registrar `DocumentAuditLog` con `Uploaded`

Regla fail-closed: si falla cualquier paso 2-4, no se guarda archivo ni metadata.

## 4. Pruebas minimas obligatorias

## Unit

- `UploadDocument_InvalidMagicBytes_Rejects`
- `UploadDocument_ScannerUnavailable_Rejects`
- `DeleteDocument_Authorized_HardDeletesFileAndRow`
- `ShareDocument_NotificationFails_DoesNotRollback`

## Integracion

- Upload exitoso con archivo valido + metadata persistida
- Upload rechazado por tamano > 25 MB
- Upload rechazado por MIME no permitido
- Upload rechazado por antivirus
- Download bloqueado por RBAC (403)
- Audit log se escribe en Upload/Delete/Replace/Share

## UI (bUnit)

- Upload page muestra progreso y mensajes
- My Documents paginacion y filtros
- Shared with Me muestra documentos recibidos

## 5. Criterio de salida

- CI verde
- Cobertura unit tests >= 80% (Application + Domain)
- Security checklist de constitucion completo
- Sin violaciones de reglas de arquitectura/seguridad/performance
