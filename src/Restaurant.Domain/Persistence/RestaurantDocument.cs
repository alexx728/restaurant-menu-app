using Restaurant.Domain.Models;
using System.Text.Json.Serialization;

namespace Restaurant.Domain.Persistence;
public sealed class RestaurantDocument
{
    [JsonRequired]
    public int Version { get; set; } = 1;

    [JsonRequired]
    public List<MenuItemData> Menu { get; set; } = [];

    [JsonRequired]
    public List<OrderData> Orders { get; set; } = [];
}

public sealed class MenuItemData
{
    [JsonRequired]
    public Guid Id { get; set; }

    [JsonRequired]
    public string Kind { get; set; } = "";

    [JsonRequired]
    public string Name { get; set; } = "";

    [JsonRequired]
    public string Category { get; set; } = "";

    [JsonRequired]
    public decimal Price { get; set; }

    [JsonRequired]
    public decimal PortionSize { get; set; }

    [JsonRequired]
    public bool IsAvailable { get; set; }

    [JsonRequired]
    public decimal DiscountPercent { get; set; }

    [JsonRequired]
    public List<Guid> ItemIds { get; set; } = [];
}

public sealed class OrderData
{
    [JsonRequired]
    public Guid Id { get; set; }

    [JsonRequired]
    public int TableNumber { get; set; }

    [JsonRequired]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonRequired]
    public OrderStatus Status { get; set; }

    [JsonRequired]
    public List<OrderLineData> Lines { get; set; } = [];
}

public sealed class OrderLineData
{
    [JsonRequired]
    public Guid Id { get; set; }

    [JsonRequired]
    public Guid MenuItemId { get; set; }

    [JsonRequired]
    public string Name { get; set; } = "";

    [JsonRequired]
    public string Category { get; set; } = "";

    [JsonRequired]
    public decimal UnitPrice { get; set; }

    [JsonRequired]
    public int Quantity { get; set; }
}
