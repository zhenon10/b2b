namespace B2B.Api.Infrastructure;

/// <summary>
/// Dış dünyaya gösterilecek API kök adresi (ör. reverse proxy arkasında <see cref="HttpRequest.Host"/> yanlışsa).
/// Boş bırakılırsa istekten türetilir.
/// </summary>
public sealed class ApiPublishingOptions
{
    public const string SectionName = "Api";

    /// <summary>Örn. <c>https://api.sirketiniz.com</c> — sonda / olmasın.</summary>
    public string PublicBaseUrl { get; set; } = "";
}
