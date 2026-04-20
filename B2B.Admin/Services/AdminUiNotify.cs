namespace B2B.Admin.Services;

public enum AdminToastKind
{
    Info,
    Warning,
    Danger
}

public sealed record AdminToast(string Title, string Message, AdminToastKind Kind);

/// <summary>
/// API handler gibi bileşen dışı kodların güvenli şekilde toast tetiklemesi (Blazor senkronizasyon bağlamı).</summary>
public sealed class AdminUiNotify
{
    private SynchronizationContext? _uiContext;

    public event Action<AdminToast>? Toast;

    /// <summary>İlk render sonrası (ör. MainLayout) çağrılmalı.</summary>
    public void AttachUiContext(SynchronizationContext? context = null) =>
        _uiContext = context ?? SynchronizationContext.Current;

    public void Raise(AdminToast toast)
    {
        void Dispatch()
        {
            Toast?.Invoke(toast);
        }

        if (_uiContext is not null)
            _uiContext.Post(_ => Dispatch(), null);
        else
            Dispatch();
    }
}
