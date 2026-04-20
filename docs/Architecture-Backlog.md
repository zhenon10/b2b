# Mimari iyileştirme öncelik raporu

Önceki kod incelemesine dayanır. Hedef: üretim güvenliği, gözlemlenebilirlik, test edilebilirlik ve uzun vadede sürdürülebilir katman ayrımı.

---

## Özet öncelik matrisi

| Öncelik | Kod | Tema | Tahmini efor |
|--------|-----|------|----------------|
| P0 | 1–3 | Üretim yüzeyi + sağlık + temel güvenlik | Düşük–orta |
| P1 | 4–6 | Test + sözleşme tutarlılığı | Orta |
| P2 | 7–9 | Mimari sadeleştirme / bulut hazırlığı | Yüksek |

---

## P0 — Hemen değer (düşük risk, yüksek fayda)

**Durum (uygulandı):** Swagger ortam kilidi, `/health` + `/health/ready`, auth uçlarında sabit pencere rate limit (IP başına 30/dk). Ayrıntılar `B2B.Api/Program.cs`, `AuthController`, `appsettings*.json`.

### 1. Swagger / OpenAPI ortam ayrımı
- **Ne:** `Development` (ve isteğe bağlı `Staging`) dışında `UseOpenApi` / `UseSwaggerUi` kapalı veya IP + auth ile kısıtlı.
- **Neden:** Prod’da API şeması ve uç listesinin açık olması bilgi sızdırır ve saldırı yüzeyini büyütür.
- **Dosya:** `B2B.Api/Program.cs`

### 2. Health endpoint’leri
- **Ne:** `GET /health` (live) ve isteğe bağlı `GET /health/ready` (DB’ye hafif ping veya EF `CanConnectAsync`).
- **Neden:** Azure / container / uptime izleme için standart; deploy sonrası doğrulama kolaylaşır.
- **Dosya:** `B2B.Api` — `Microsoft.Extensions.Diagnostics.HealthChecks` + `MapHealthChecks`.

### 3. Rate limiting (kritik uçlar)
- **Ne:** Özellikle `POST .../auth/login`, `register`, `refresh` (ve gerekirse `change-password`) için IP veya kullanıcı başına sınırlar (`AspNetCoreRateLimit` veya .NET 8+ built-in rate limiter).
- **Neden:** Brute-force ve credential stuffing riskini azaltır.
- **Dosya:** `B2B.Api/Program.cs`, ilgili `AuthController` route’ları.

---

## P1 — Kalite ve regresyon riskini düşürme

**Durum (uygulandı — P1-4, P1-5, P1-6):** Entegrasyon testleri `B2B.Api.Tests` altında: `CriticalFlowIntegrationTests` (bayi token ile admin uçlarına 403, sipariş `Idempotency-Key` ile tekrarlı gönderim, boş `Items` veya doğrulama 400) ve mevcut `ApiScenariosTests` / health senaryoları. İsimli yetkilendirme: `B2B.Api/Security/AuthorizationPolicies.cs` (`AdminOnly`, `DealerOnly`), kayıt `B2B.Api/Program.cs` içinde `AddAuthorization`, controller’larda `[Authorize(Policy = ...)]` (admin uçları ve `OrdersController` bayi uçları). **Paylaşımlı sözleşme:** `B2B.Contracts` projesi (zarf `ApiResponse`/`ApiError`, sayfalama, auth/ürün/kategori/sipariş/admin DTO’ları; `OrderStatus` için `B2B.Domain` referansı). `B2B.Api`, `B2B.Api.Tests`, `B2B.Mobile`, `B2B.Admin` bu projeye referans verir; API controller’lar ile mobil/admin istemciler aynı tipleri kullanır.

### 4. API entegrasyon testleri (kritik akışlar)
- **Ne:** `WebApplicationFactory` ile auth (login/refresh), sipariş gönderimi (idempotency header ile çift gönderim), admin-only uç (403), validation 400.
- **Neden:** Controller + EF doğrudan modelinde refactor maliyeti yüksek; güvenlik ağı.
- **Dosya:** `B2B.Api.Tests`

### 5. Yetkilendirme: isimli policy’ler
- **Ne:** `RequireRole("Admin")` / `Dealer` için `AuthorizationOptions` altında `AdminOnly`, `DealerOnly` gibi policy’ler; `[Authorize(Policy = "...")]`.
- **Neden:** Rol dağılımı tek yerde; ileride claim tabanlı kurallar eklemek kolaylaşır.
- **Dosya:** `B2B.Api/Program.cs`, controller’lar.

### 6. İstemci–API sözleşmesi (drift azaltma)
- **Ne (seçeneklerden biri):** (A) Küçük paylaşımlı `B2B.Contracts` / `B2B.Api.Client` projesi, (B) NSwag/OpenAPI’den C# client üretimi (Mobil + Admin).
- **Neden:** `B2BApiClient` record’ları ile mobil DTO’ların ayrışma riski.
- **Dosya:** Yeni proje veya build adımı; `B2B.Admin`, `B2B.Mobile` tüketimi.

---

## P2 — Mimari borç ve bulut/ölçek (daha büyük işler)

### 7. Use case / Application servis katmanı
- **Ne:** Öncelikle en karmaşık akışlar (`OrdersController.Submit`, ürün güncelleme/stok) için `B2B.Application` altında servisler; controller ince, transaction sınırları net.
- **Neden:** Test, tekrar kullanım (ör. arka plan job), okunabilirlik.
- **Dosya:** `B2B.Application`, `B2B.Api/Controllers`

### 8. Repository soyutlaması (ihtiyaç halinde)
- **Ne:** Sadece test edilebilirlik veya birden fazla veri kaynağı ihtiyacı doğarsa `IOrderRepository` vb.; her endpoint için zorunlu değil.
- **Neden:** Şu an `DbContext` yeterli; aşırı soyutlama erken olabilir.

### 9. Dosya yükleme → nesne depolama (Azure’a geçince)
- **Ne:** `UploadsController` disk yerine Blob + URL stratejisi; şimdilik ertelenebilir (online planı askıda).
- **Neden:** Ölçek ve çok örnekli App Service ile disk uyumsuzluğu.

---

## Blazor Admin / MAUI çift yönetim (ürün kararı — kod önceliği değil)

- İki yüzeyde aynı admin işlemleri varsa: ya **tekincil** belirlenir ya da özellik seti ayrılır (dokümantasyon).
- Bu madde öncelik listesinden çıkarılabilir; iş analizi sonrası backlog’a eklenir.

---

## Önerilen uygulama sırası (sprint mantığı)

1. **Sprint 1:** P0-1 (Swagger) + P0-2 (Health)  
2. **Sprint 2:** P0-3 (Rate limit) + P1-4 (testlerin ilk paketi)  
3. **Sprint 3:** P1-5 (policy) + P1-6 (contracts veya client gen) — ihtiyaca göre  
4. **Backlog:** P2-7 → 8 → 9 (ihtiyaç ve Azure ile hizalanır)

---

*Son güncelleme: P1-6 (`B2B.Contracts`) sonrası; mimari inceleme sonrası oluşturuldu.*
