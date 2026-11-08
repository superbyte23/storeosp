namespace StoreOps.Application.Contracts.Categories;

public sealed record CategoryRequest(string Name);
public sealed record CategoryDto(Guid Id, string Name);
