namespace StoreOps.Application.Contracts.Reports;

public sealed record DashboardDto(
    int ProductCount,
    int LowStockCount,
    int PendingOrderCount,
    decimal FulfilledRevenue);
