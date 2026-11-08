namespace StoreOps.Application.Contracts.Products;

public sealed record SaveProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Description,
    decimal UnitPrice,
    int StockQuantity);

public sealed record ProductDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Sku,
    string Name,
    string Description,
    decimal UnitPrice,
    int StockQuantity,
    DateTime CreatedAtUtc);
