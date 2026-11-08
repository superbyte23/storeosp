using Microsoft.EntityFrameworkCore;
using StoreOps.Application.Contracts.Reports;
using StoreOps.Domain.Entities;
using StoreOps.Infrastructure.Persistence;

namespace StoreOps.Api.Endpoints;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/reports").RequireAuthorization("ViewReports");
        group.MapGet("/dashboard", async (StoreOpsDbContext db, CancellationToken cancellationToken) =>
        {
            var productCount = await db.Products.CountAsync(cancellationToken);
            var lowStockCount = await db.Products.CountAsync(
                product => product.StockQuantity <= 5, cancellationToken);
            var pendingOrderCount = await db.Orders.CountAsync(
                order => order.Status == OrderStatus.Pending, cancellationToken);
            var fulfilledRevenue = await db.OrderItems
                .Where(item => item.Order.Status == OrderStatus.Fulfilled)
                .SumAsync(item => (decimal?)(item.Quantity * item.UnitPrice), cancellationToken) ?? 0m;
            return Results.Ok(new DashboardDto(
                productCount, lowStockCount, pendingOrderCount, fulfilledRevenue));
        });
        return group;
    }
}
