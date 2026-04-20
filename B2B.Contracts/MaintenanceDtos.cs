namespace B2B.Contracts;

public sealed record BrokenProductImage(Guid ProductImageId, Guid ProductId, string Url);

public sealed record ReconcileProductImagesResponse(
    bool DryRun,
    string WebRoot,
    string UploadsRoot,
    int TotalImages,
    int BrokenCount,
    int DeletedCount,
    IReadOnlyList<BrokenProductImage> Broken);
