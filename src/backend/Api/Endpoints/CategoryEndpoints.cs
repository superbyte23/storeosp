using Microsoft.EntityFrameworkCore;
using StoreOps.Application.Contracts.Categories;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Persistence;

namespace StoreOps.Api.Endpoints;

public static class CategoryEndpoints
{
    public static RouteGroupBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/categories").RequireAuthorization();

        group.MapGet("/", async (StoreOpsDbContext db, CancellationToken cancellationToken) =>
            await db.Categories.AsNoTracking()
                .OrderBy(category => category.Name)
                .Select(category => new CategoryDto(category.Id, category.Name))
                .ToListAsync(cancellationToken));

        group.MapPost("/", async (
            CategoryRequest request,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            var name = request.Name.Trim();
            if (name.Length is < 1 or > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["Name must be 1–100 characters."]
                });

            if (await db.Categories.AnyAsync(category => category.Name == name, cancellationToken))
                return Results.Conflict(new { title = "A category with this name already exists." });

            var category = new Category { Name = name };
            db.Categories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/categories/{category.Id}",
                new CategoryDto(category.Id, category.Name));
        }).RequireAuthorization("ManageProducts");

        return group;
    }
}
