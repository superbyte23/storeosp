using Microsoft.EntityFrameworkCore;
using StoreOps.Application.Contracts.Products;
using StoreOps.Application.Contracts.Shared;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Persistence;

namespace StoreOps.Api.Endpoints;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/products").RequireAuthorization();

        group.MapGet("/", async (
            StoreOpsDbContext db,
            int? page,
            int? pageSize,
            string? search,
            Guid? categoryId,
            string? sortBy,
            bool? descending,
            CancellationToken cancellationToken) =>
        {
            var currentPage = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var query = db.Products.AsNoTracking().AsQueryable();

            if (categoryId is not null)
                query = query.Where(product => product.CategoryId == categoryId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(product => product.Name.Contains(term) || product.Sku.Contains(term));
            }

            query = (sortBy?.ToLowerInvariant(), descending == true) switch
            {
                ("price", false) => query.OrderBy(product => product.UnitPrice),
                ("price", true) => query.OrderByDescending(product => product.UnitPrice),
                ("stock", false) => query.OrderBy(product => product.StockQuantity),
                ("stock", true) => query.OrderByDescending(product => product.StockQuantity),
                ("sku", false) => query.OrderBy(product => product.Sku),
                ("sku", true) => query.OrderByDescending(product => product.Sku),
                (_, true) => query.OrderByDescending(product => product.Name),
                _ => query.OrderBy(product => product.Name)
            };

            var total = await query.LongCountAsync(cancellationToken);
            var items = await query.Skip((currentPage - 1) * size)
                .Take(size)
                .Select(product => new ProductDto(
                    product.Id, product.CategoryId, product.Category.Name, product.Sku, product.Name,
                    product.Description, product.UnitPrice, product.StockQuantity, product.CreatedAtUtc))
                .ToListAsync(cancellationToken);
            return Results.Ok(new PagedResult<ProductDto>(items, currentPage, size, total));
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var product = await db.Products.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new ProductDto(
                    item.Id, item.CategoryId, item.Category.Name, item.Sku, item.Name,
                    item.Description, item.UnitPrice, item.StockQuantity, item.CreatedAtUtc))
                .SingleOrDefaultAsync(cancellationToken);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapPost("/", async (
            SaveProductRequest request,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors is not null)
                return Results.ValidationProblem(errors);
            if (!await db.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoryId"] = ["Category does not exist."]
                });
            if (await db.Products.AnyAsync(product => product.Sku == request.Sku.Trim(), cancellationToken))
                return Results.Conflict(new { title = "SKU already exists." });

            var product = new Product();
            Apply(product, request);
            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/products/{product.Id}",
                await GetDtoAsync(db, product.Id, cancellationToken));
        }).RequireAuthorization("ManageProducts");

        group.MapPut("/{id:guid}", async (
            Guid id,
            SaveProductRequest request,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var errors = Validate(request);
            if (errors is not null)
                return Results.ValidationProblem(errors);

            var product = await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (product is null)
                return Results.NotFound();
            if (!await db.Categories.AnyAsync(category => category.Id == request.CategoryId, cancellationToken))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["categoryId"] = ["Category does not exist."]
                });
            if (await db.Products.AnyAsync(
                    item => item.Id != id && item.Sku == request.Sku.Trim(), cancellationToken))
                return Results.Conflict(new { title = "SKU already exists." });

            Apply(product, request);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await GetDtoAsync(db, id, cancellationToken));
        }).RequireAuthorization("ManageProducts");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var product = await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (product is null)
                return Results.NotFound();
            if (await db.OrderItems.AnyAsync(item => item.ProductId == id, cancellationToken))
                return Results.Conflict(new { title = "Product is referenced by an order." });

            db.Products.Remove(product);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization("ManageProducts");

        return group;
    }

    private static async Task<ProductDto?> GetDtoAsync(
        StoreOpsDbContext db,
        Guid id,
        CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Where(product => product.Id == id)
            .Select(product => new ProductDto(
                product.Id, product.CategoryId, product.Category.Name, product.Sku, product.Name,
                product.Description, product.UnitPrice, product.StockQuantity, product.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

    private static Dictionary<string, string[]>? Validate(SaveProductRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.CategoryId == Guid.Empty)
            errors["categoryId"] = ["A category is required."];
        if (string.IsNullOrWhiteSpace(request.Sku) || request.Sku.Trim().Length > 64)
            errors["sku"] = ["SKU is required and must be at most 64 characters."];
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            errors["name"] = ["Name is required and must be at most 200 characters."];
        if (request.Description?.Length > 2000)
            errors["description"] = ["Description must be at most 2000 characters."];
        if (request.UnitPrice < 0)
            errors["unitPrice"] = ["Price cannot be negative."];
        if (request.StockQuantity < 0)
            errors["stockQuantity"] = ["Stock cannot be negative."];
        return errors.Count == 0 ? null : errors;
    }

    private static void Apply(Product product, SaveProductRequest request)
    {
        product.CategoryId = request.CategoryId;
        product.Sku = request.Sku.Trim();
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim() ?? string.Empty;
        product.UnitPrice = request.UnitPrice;
        product.StockQuantity = request.StockQuantity;
    }
}
