using Microsoft.EntityFrameworkCore;
using StoreOps.Application.Contracts.Orders;
using StoreOps.Application.Contracts.Shared;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Persistence;

namespace StoreOps.Api.Endpoints;

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/orders").RequireAuthorization("ManageOrders");

        group.MapGet("/", async (
            StoreOpsDbContext db,
            int? page,
            int? pageSize,
            string? status,
            CancellationToken cancellationToken) =>
        {
            var currentPage = Math.Max(1, page ?? 1);
            var size = Math.Clamp(pageSize ?? 20, 1, 100);
            var query = db.Orders.AsNoTracking().Include(order => order.Items).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<OrderStatus>(status, true, out var parsed))
                query = query.Where(order => order.Status == parsed);
            else if (!string.IsNullOrWhiteSpace(status))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Invalid order status."]
                });

            var total = await query.LongCountAsync(cancellationToken);
            var orders = await query.OrderByDescending(order => order.CreatedAtUtc)
                .Skip((currentPage - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);
            var items = orders.Select(ToDto).ToList();
            return Results.Ok(new PagedResult<OrderDto>(items, currentPage, size, total));
        });

        group.MapPost("/", async (
            CreateOrderRequest request,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (request.Items.Count == 0 ||
                request.Items.Any(item => item.Quantity is < 1 or > 10000) ||
                request.Items.Select(item => item.ProductId).Distinct().Count() != request.Items.Count)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = ["Provide unique products with valid quantities."]
                });

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var productIds = request.Items.Select(item => item.ProductId).ToArray();
            var products = await db.Products.Where(product => productIds.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id, cancellationToken);
            if (products.Count != request.Items.Count)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["items"] = ["One or more products do not exist."]
                });

            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];
                if (product.StockQuantity < item.Quantity)
                    return Results.Conflict(new { title = $"Insufficient stock for {product.Name}." });
            }

            var order = new Order();
            foreach (var item in request.Items)
            {
                var product = products[item.ProductId];
                product.StockQuantity -= item.Quantity;
                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = product.UnitPrice
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Created($"/api/v1/orders/{order.Id}", ToDto(order));
        });

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            OrderStatusRequest request,
            StoreOpsDbContext db,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<OrderStatus>(request.Status, true, out var next))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Invalid order status."]
                });

            var order = await db.Orders.Include(item => item.Items)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (order is null)
                return Results.NotFound();
            if (order.Status != OrderStatus.Pending || next == OrderStatus.Pending)
                return Results.Conflict(new { title = "Only pending orders can be fulfilled or cancelled." });

            if (next == OrderStatus.Cancelled)
            {
                foreach (var item in order.Items)
                {
                    var product = await db.Products.SingleOrDefaultAsync(
                        candidate => candidate.Id == item.ProductId, cancellationToken);
                    if (product is not null)
                        product.StockQuantity += item.Quantity;
                }
            }

            order.Status = next;
            order.FulfilledAtUtc = next == OrderStatus.Fulfilled ? DateTime.UtcNow : null;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ToDto(order));
        });

        return group;
    }

    private static OrderDto ToDto(Order order)
    {
        var items = order.Items.Select(item =>
            new OrderItemDto(item.ProductId, item.ProductName, item.Quantity, item.UnitPrice)).ToArray();
        return new OrderDto(order.Id, order.Status, order.CreatedAtUtc, order.FulfilledAtUtc,
            items.Sum(item => item.Quantity * item.UnitPrice), items);
    }
}
