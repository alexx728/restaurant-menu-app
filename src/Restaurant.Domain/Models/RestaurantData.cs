namespace Restaurant.Domain.Models;
public sealed record RestaurantData(IReadOnlyList<MenuItem> Menu, IReadOnlyList<Order> Orders);
