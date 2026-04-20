using B2B.Mobile.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;

namespace B2B.Mobile.Features.Auth.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ApplicationApiSessionState _sessionApi;

    [ObservableProperty] private string apiUrlDraft = "";
    [ObservableProperty] private string sessionBaseUrl = "";
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isTesting;

    public SettingsViewModel(ApplicationApiSessionState sessionApi) => _sessionApi = sessionApi;

    [RelayCommand]
    private void LoadDraft()
    {
        SessionBaseUrl = _sessionApi.SessionResolvedBaseUrl;
        try
        {
            ApiUrlDraft = Preferences.Default.Get(MobilePreferenceKeys.ApiBaseUrlOverride, "") ?? "";
        }
        catch
        {
            ApiUrlDraft = "";
        }

        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SaveOverrideAsync()
    {
        StatusMessage = null;
        var trimmed = (ApiUrlDraft ?? "").Trim();
        if (trimmed.Length == 0)
        {
            try
            {
                Preferences.Default.Remove(MobilePreferenceKeys.ApiBaseUrlOverride);
            }
            catch { }

            StatusMessage = "Özel adres kaldırıldı. Bir sonraki açılışta yapılandırma sırası kullanılacak.";
            await AlertAsync("Kaydedildi", "Değişikliklerin uygulanması için uygulamayı tamamen kapatıp yeniden açın.");
            return;
        }

        if (!ApiBaseUrlResolver.TryNormalizeUserBaseUrl(trimmed, out var normalized))
        {
            await AlertAsync("Geçersiz adres", "http veya https ile başlayan bir kök URL girin (örn. https://api.sirketim.com/).");
            return;
        }

        try
        {
            Preferences.Default.Set(MobilePreferenceKeys.ApiBaseUrlOverride, normalized);
        }
        catch (Exception ex)
        {
            await AlertAsync("Kayıt hatası", ex.Message);
            return;
        }

        ApiUrlDraft = normalized.TrimEnd('/');
        StatusMessage = $"Kayıtlı adres: {normalized}";
        await AlertAsync(
            "Kaydedildi",
            "Yeni API adresi bir sonraki uygulama başlatılışında kullanılır. Şimdi denemek için uygulamayı kapatıp yeniden açın.");
    }

    [RelayCommand]
    private async Task ClearOverrideAsync()
    {
        ApiUrlDraft = "";
        try
        {
            Preferences.Default.Remove(MobilePreferenceKeys.ApiBaseUrlOverride);
        }
        catch { }

        StatusMessage = "Özel adres temizlendi.";
        await AlertAsync("Kaydedildi", "Varsayılan çözümleme bir sonraki açılışta uygulanır. Gerekirse uygulamayı yeniden başlatın.");
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (IsTesting) return;
        var raw = (ApiUrlDraft ?? "").Trim();
        if (raw.Length == 0)
            raw = _sessionApi.SessionResolvedBaseUrl;

        if (!ApiBaseUrlResolver.TryNormalizeUserBaseUrl(raw, out var baseUrl))
        {
            await AlertAsync("Geçersiz adres", "Önce geçerli bir kök URL girin veya oturum adresini kullanmak için alanı boş bırakın.");
            return;
        }

        IsTesting = true;
        StatusMessage = null;
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };

            using var resp = await client.GetAsync(new Uri("api/v1/health", UriKind.Relative), CancellationToken.None);
            if (!resp.IsSuccessStatusCode)
            {
                StatusMessage = $"Sunucu {(int)resp.StatusCode} döndü.";
                await AlertAsync("Bağlantı", $"Ulaşıldı ancak beklenen yanıt alınamadı ({(int)resp.StatusCode}).");
                return;
            }

            StatusMessage = "Bağlantı başarılı (health).";
            await AlertAsync("Bağlantı", "API kök adresine ulaşıldı ve sağlık uç noktası yanıt verdi.");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            await AlertAsync("Bağlantı hatası", ex.Message);
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private static async Task CopySessionUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        await Clipboard.Default.SetTextAsync(url);
        await AlertAsync("Panoya kopyalandı", "Şu anki oturum adresi panoya alındı.");
    }

    private static Task AlertAsync(string title, string message)
    {
        var page = Shell.Current?.CurrentPage;
        return page is null ? Task.CompletedTask : page.DisplayAlertAsync(title, message, "Tamam");
    }
}
