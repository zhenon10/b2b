# Mobile release (Android) via GitHub Actions

This repo publishes a **signed Android AAB + APK** when you push a tag matching `v*.*.*` (example: `v1.2.3`).

## 1) Create an Android keystore (once)

Run on your machine (Windows PowerShell example). Pick strong passwords and keep them safe.

```powershell
mkdir keystore
cd keystore

keytool -genkeypair -v `
  -keystore b2b-mobile.jks `
  -storetype JKS `
  -keyalg RSA `
  -keysize 2048 `
  -validity 3650 `
  -alias b2b `
  -storepass "<STORE_PASS>" `
  -keypass "<KEY_PASS>" `
  -dname "CN=B2B, OU=Mobile, O=B2B, L=Istanbul, S=Istanbul, C=TR"
```

Convert the keystore to base64 (PowerShell):

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\\b2b-mobile.jks")) | Set-Content -NoNewline .\\b2b-mobile.jks.b64
```

## 2) Add GitHub repository secrets

In GitHub: **Settings → Secrets and variables → Actions → New repository secret**

- `ANDROID_KEYSTORE_BASE64`: content of `b2b-mobile.jks.b64`
- `ANDROID_KEYSTORE_PASSWORD`: `<STORE_PASS>`
- `ANDROID_KEY_ALIAS`: `b2b` (or your alias)
- `ANDROID_KEY_PASSWORD`: `<KEY_PASS>`

## 3) Release by tagging

```bash
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will build and create a GitHub Release containing:
- `*-Signed.aab` (Play Store upload)
- `*-Signed.apk` (direct install)

