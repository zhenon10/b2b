## Admin bildirim sistemi (Push + Inbox)

Bu doküman; admin’in tüm kullanıcılara bildirim gönderebilmesi (push) ve kullanıcıların uygulama içi “Bildirimler” ekranından gelen mesajları görebilmesi için gereken kurulum adımlarını özetler.

### 1) API (B2B.Api) – ortam değişkenleri

- **Push’u aç/kapat**:
  - `Push__Enabled=true`

- **Firebase Admin (service account JSON)**:
  - `Push__Fcm__ServiceAccountJson=<JSON>`
  - (Opsiyonel) `Push__Fcm__ProjectId=<project-id>`

Notlar:
- `ServiceAccountJson` **gizlidir**. Dosya yerine environment variable / secret manager kullanın.
- Push kapalıysa (`Push__Enabled=false`) admin endpoint’i yine bildirimi DB’ye yazar; sadece push gönderimi yapılmaz.

### 2) API – endpoint’ler

- **Token kayıt** (mobil cihaz token’ı):
  - `POST /api/v1/push-tokens`
  - body: `{ "token": "...", "platform": "Android" }`

- **Admin broadcast oluşturma**:
  - `POST /api/v1/admin/notifications`
  - body: `{ "title": "...", "body": "...", "dataJson": "{...}" }`

- **Kullanıcı inbox**:
  - `GET /api/v1/notifications?page=1&pageSize=30`

- **Okundu işaretleme**:
  - `POST /api/v1/notifications/{notificationId}/read`

### 3) Mobil (B2B.Mobile) – Firebase kurulum

Bu repo artık `Plugin.Firebase` kullanır.

#### Android

1. Firebase Console’dan Android uygulamasını ekleyin.
2. `google-services.json` dosyasını `B2B.Mobile/` proje köküne koyun.
3. Build Action otomatik: csproj içinde `GoogleServicesJson` olarak işlenir (dosya varsa).
4. Uygulama açılışında token alınır ve API’ye gönderilir:
   - `POST /api/v1/push-tokens`

Notlar:
- Android minimum sürüm 23’e yükseltildi.
- Push bildiriminin cihazda görünmesi OS ayarlarına ve izinlere bağlıdır.

### 4) Mobil – Inbox ekranı

- Profil → **Bildirimler** ekranı:
  - `GET /api/v1/notifications`
  - Mesaja tıklayınca `POST /api/v1/notifications/{id}/read` çağrılır.

