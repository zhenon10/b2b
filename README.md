# b2b

## Local dev: MinIO (S3-compatible uploads)

This repo supports storing uploads either on local disk (`wwwroot/uploads`) or an S3-compatible object store.

### Option A: Docker Desktop (recommended)

From repo root:

```bash
docker compose -f docker-compose.minio.yml up -d
```

- **MinIO Console**: `http://localhost:9001` (user: `minioadmin`, pass: `minioadmin123`)
- Bucket `b2b-uploads` is created automatically and set to anonymous download for local testing.

Run the API with environment variables (PowerShell example):

```powershell
$env:ObjectStorage__Provider="S3"
$env:ObjectStorage__Endpoint="http://localhost:9000"
$env:ObjectStorage__ForcePathStyle="true"
$env:ObjectStorage__Region="us-east-1"
$env:ObjectStorage__Bucket="b2b-uploads"
$env:ObjectStorage__AccessKeyId="minioadmin"
$env:ObjectStorage__SecretAccessKey="minioadmin123"
$env:ObjectStorage__PublicBaseUrl="http://localhost:9000/b2b-uploads"

cd B2B.Api
dotnet run
```

### Smoke test

Upload a product image via `POST /api/v1/uploads/images` and verify:

- The API response URL is under `ObjectStorage__PublicBaseUrl`
- The object exists in MinIO under `b2b-uploads/uploads/products/...`