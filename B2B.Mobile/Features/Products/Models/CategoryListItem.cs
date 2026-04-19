namespace B2B.Mobile.Features.Products.Models;

public sealed record CategoryListItem(Guid CategoryId, string Name, int SortOrder, bool IsActive);
