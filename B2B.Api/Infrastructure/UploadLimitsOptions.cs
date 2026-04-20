namespace B2B.Api.Infrastructure;

public sealed class UploadLimitsOptions
{
    public const string SectionName = "Uploads";

    /// <summary>Max uploads per user per UTC day.</summary>
    public int DailyMaxCount { get; set; } = 200;

    /// <summary>Max total stored bytes per user per UTC day.</summary>
    public long DailyMaxBytes { get; set; } = 200L * 1024 * 1024;
}

