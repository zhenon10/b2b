namespace B2B.Api.Infrastructure;

public interface IObjectStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);

    /// <summary>Return a public URL for a stored key.</summary>
    string GetPublicUrl(string key, HttpRequest request);
}

