## VPS deploy (Ubuntu) — domain olmadan (Tailscale ile)

Bu repo `net10.0` kullandığı için VPS’de en pratik yayınlama yöntemi Docker Compose’tur.

### 0) Ön koşullar
- Sunucuda Docker + Compose plugin
- Sunucuda Tailscale (uzaktan erişim için)

### 1) Sunucuda repo’yu klonla
```bash
sudo mkdir -p /opt/b2b
sudo chown -R $USER:$USER /opt/b2b
cd /opt/b2b
git clone <REPO_URL> .
```

### 2) Ortam değişkenlerini ayarla
```bash
cp deploy/.env.example .env
nano .env
```

En az şu 3 değer dolu olmalı:
- `MSSQL_SA_PASSWORD`
- `CONNECTIONSTRINGS__SQLSERVER`
- `JWT__SIGNINGKEY`

### 3) Servisleri ayağa kaldır
```bash
docker compose up -d --build
docker compose ps
docker compose logs -f api
```

### 4) Erişim
- Tailscale IP’nizi bulun:
```bash
tailscale ip -4
```
- API:
  - `http://<TAILSCALE_IP>:8080`
- Health:
  - `http://<TAILSCALE_IP>:8080/health`
  - `http://<TAILSCALE_IP>:8080/health/ready`

### Notlar
- Upload dosyaları `b2b_uploads` volume’unda kalıcıdır (`/app/wwwroot/uploads`).
- DB `b2b_mssql` volume’unda kalıcıdır.
- Prod guardrail’ları nedeniyle `Seed:Mode`, `Database:ApplyMigrationsOnStartup`, `Api:EnableSwagger` gibi ayarları Production’da açmayın.

