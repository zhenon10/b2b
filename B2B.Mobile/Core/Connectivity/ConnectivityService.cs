using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace B2B.Mobile.Core.Connectivity;

/// <summary>Ağ erişim durumu; <see cref="MainHeaderViewModel"/> ve benzeri bileşenler için.</summary>
public sealed class ConnectivityService : INotifyPropertyChanged, IDisposable
{
    public ConnectivityService()
    {
        global::Microsoft.Maui.Networking.Connectivity.ConnectivityChanged += OnConnectivityChanged;
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>İnternet erişimi yok veya bilinmiyor (kullanıcıya uyarı göster).</summary>
    public bool IsOffline { get; private set; }

    /// <summary>
    /// Ölçülü / dar kanal olasılığı (yalnızca bilgi). SDK’da <c>NetworkAccess.Constrained</c> olmadığı için
    /// yalnızca hücresel profil kullanımına göre kabaca işaretlenir.
    /// </summary>
    public bool IsConstrained { get; private set; }

    public void Dispose() =>
        global::Microsoft.Maui.Networking.Connectivity.ConnectivityChanged -= OnConnectivityChanged;

    private void OnConnectivityChanged(object? sender, global::Microsoft.Maui.Networking.ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(Refresh);

    private void Refresh()
    {
        var c = global::Microsoft.Maui.Networking.Connectivity.Current;
        var access = c.NetworkAccess;
        var offline = access is global::Microsoft.Maui.Networking.NetworkAccess.None;

        var profiles = c.ConnectionProfiles.ToList();
        var constrained =
            !offline
            && profiles.Count > 0
            && profiles.All(p => p == global::Microsoft.Maui.Networking.ConnectionProfile.Cellular);

        if (IsOffline == offline && IsConstrained == constrained)
            return;

        IsOffline = offline;
        IsConstrained = constrained;
        OnPropertyChanged(nameof(IsOffline));
        OnPropertyChanged(nameof(IsConstrained));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
