## 1. Storage module

- [x] 1.1 Add pinned refs `Platform.Storage`, `Platform.Storage.Local`, `Platform.Storage.S3`; add `Storage:Provider|Local|S3` config
- [x] 1.2 Implement `StorageModule` selector; local atomic root `/app/data/objects`; key validation

## 2. Swap call sites

- [x] 2.1 Migrate `QuestPdfRenderer` output + download handlers to `IObjectStorage`; add `Accept-Ranges`/`206`
- [x] 2.2 Migrate `BackupCoordinator` bundle I/O; extend `/status` with provider health (secret-free)

## 3. Verify

- [x] 3.1 `dotnet build` 0/0; finalize->download byte-identical local + MinIO
- [x] 3.2 Range `206` + presigned automation fetch smoke; `openspec validate --change platform-storage-abstraction --strict`
