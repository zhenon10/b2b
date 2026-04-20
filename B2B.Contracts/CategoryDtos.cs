namespace B2B.Contracts;

public sealed record CategoryListItem(Guid CategoryId, string Name, int SortOrder, bool IsActive);
public sealed record CreateCategoryRequest(string Name, int SortOrder = 0, bool IsActive = true);
public sealed record UpdateCategoryRequest(string Name, int SortOrder, bool IsActive);
