namespace B2B.Api.Push;

public sealed class PushOptions
{
    public const string SectionName = "Push";

    public bool Enabled { get; set; }
}

public sealed class FcmOptions
{
    public const string SectionName = "Push:Fcm";

    /// <summary>Firebase service account JSON (secret). Prefer env var injection.</summary>
    public string? ServiceAccountJson { get; set; }

    /// <summary>Optional; can be inferred from service account JSON.</summary>
    public string? ProjectId { get; set; }
}

