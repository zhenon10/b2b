namespace B2B.Mobile.Core;

/// <summary>Lightweight pub/sub for catalog-related UI refresh (no MessagingCenter in ViewModels).</summary>
public sealed class CatalogNotifications
{
    public event EventHandler? SessionChanged;
    public event EventHandler? CategoriesChanged;

    public void NotifySessionChanged() => SessionChanged?.Invoke(this, EventArgs.Empty);

    public void NotifyCategoriesChanged() => CategoriesChanged?.Invoke(this, EventArgs.Empty);
}
