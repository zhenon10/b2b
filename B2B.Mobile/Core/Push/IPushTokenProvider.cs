namespace B2B.Mobile.Core.Push;

public interface IPushTokenProvider
{
    Task<string?> TryGetTokenAsync(CancellationToken ct = default);
}

