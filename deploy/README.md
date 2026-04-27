## VPS deploy (Ubuntu) — domain olmadan (Tailscale ile)

Bu repo `net10.0` kullandığı için VPS’de en pratik yayınlama yöntemi Docker Compose’tur.

### 0) Ön koşullar
- Sunucuda Docker + Compose plugin
- (Opsiyonel) Sunucuda Tailscale (private erişim için)

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

İsteğe bağlı ama prod’da önerilir:
- `API__PUBLICBASEURL` (mutlak URL üretimi için)
- `CORS__ALLOWEDORIGINS__0..2` (browser tabanlı istemciler için)

### 3) Servisleri ayağa kaldır
```bash
docker compose up -d --build
docker compose ps
docker compose logs -f api
```

### 4) Erişim
- Eğer public IP ile gidecekseniz:
  - API: `http://<VPS_PUBLIC_IP>:8080`
- Tailscale ile private erişim isterseniz Tailscale IP’nizi bulun:
```bash
tailscale ip -4
```
  - API: `http://<TAILSCALE_IP>:8080`
- Health:
  - `http://<TAILSCALE_IP>:8080/health`
  - `http://<TAILSCALE_IP>:8080/health/ready`

### 5) Public IP ile önerilen env değerleri
`.env` içinde aşağıdakileri public IP’ye göre ayarlayın:
- `API__PUBLICBASEURL=http://<VPS_PUBLIC_IP>:8080`
- Eğer upload URL’lerinin absolute dönmesini istiyorsanız (Local provider):
  - `OBJECTSTORAGE__PUBLICBASEURL=http://<VPS_PUBLIC_IP>:8080/uploads`
- Browser tabanlı bir istemciniz varsa ilgili origin’i ekleyin:
  - `CORS__ALLOWEDORIGINS__0=https://...` veya `http://...`

### Notlar
- Upload dosyaları `b2b_uploads` volume’unda kalıcıdır (`/app/wwwroot/uploads`).
- DB `b2b_mssql` volume’unda kalıcıdır.
- Prod guardrail’ları nedeniyle `Seed:Mode`, `Database:ApplyMigrationsOnStartup`, `Api:EnableSwagger` gibi ayarları Production’da açmayın.

## Domain + SSL (opsiyonel)

Domain ile yayınlamak istiyorsanız genelde en rahat yöntem reverse proxy (Caddy/Nginx) ile 80/443’ten API’ye yönlendirmektir.
Bu repo, API’yi container içinde `:8080`’de çalıştıracak şekilde ayarlı; proxy 443’ten terminasyon yapıp içeriye proxy’ler.

### Caddy ile hızlı kurulum (önerilir)

1) DNS’te bir A kaydı oluşturun (ör. `api.example.com` -> VPS public IP)

2) `deploy/Caddyfile` içinde domain’i açın:
- `api.example.com { reverse_proxy api:8080 }`

3) Compose’u iki dosya ile başlatın:
```bash
docker compose -f docker-compose.yml -f docker-compose.caddy.yml up -d --build
docker compose ps
docker compose logs -f caddy
```

Not: Caddy, Let’s Encrypt ile otomatik sertifika alır; VPS’in 80/443 portları açık olmalı.

## Canlı kullanım için önerilen minimum (evde Ubuntu + Tailscale)

### 1) Otomatik açılış (systemd)

Bu repo `deploy/systemd/b2b-compose.service` ile boot sonrası `docker compose up -d` çalıştırabilir.

Ubuntu'da:
```bash
cd /opt/b2b
sudo cp deploy/systemd/b2b-compose.service /etc/systemd/system/b2b-compose.service
sudo systemctl daemon-reload
sudo systemctl enable --now b2b-compose.service
sudo systemctl status b2b-compose.service
```

### 2) MSSQL günlük yedek (systemd timer)

- Yedekler host üzerinde `/opt/b2b/backups/` altında `.bak` olarak tutulur.
- Varsayılan retention: 7 gün (`RETENTION_DAYS`).

Kurulum:
```bash
cd /opt/b2b
sudo chmod +x deploy/scripts/mssql_backup.sh

sudo cp deploy/systemd/b2b-mssql-backup.service /etc/systemd/system/b2b-mssql-backup.service
sudo cp deploy/systemd/b2b-mssql-backup.timer /etc/systemd/system/b2b-mssql-backup.timer
sudo systemctl daemon-reload
sudo systemctl enable --now b2b-mssql-backup.timer
systemctl list-timers | grep b2b-mssql-backup
```

Manuel test:
```bash
sudo systemctl start b2b-mssql-backup.service
ls -la /opt/b2b/backups
```

