using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;
public sealed record MenuRow(MenuItem Item)
{
    public string Name => Item.Name;
    public string Category => Item.Category;
    public string Kind => Item switch
    {
        Dish => "Страва",
        Drink => "Напій",
        _ => "Комбо"
    };
    public decimal Price => Item.Price;
    public decimal FinalPrice => Item.CalculatePrice();
    public string Portion => $"{Item.PortionSize} {Item.PortionUnit}";
    public bool IsAvailable => Item.IsAvailable;
}

public sealed record OrderRow(Order Item)
{
    public int Table => Item.TableNumber;
    public DateTimeOffset CreatedAt => Item.CreatedAt;
    public string Status => StatusChoice.All.First(x => x.Value == Item.Status).Name;
    public decimal Total => Item.Total;
}

public sealed record StatusChoice(OrderStatus? Value, string Name)
{
    public override string ToString() => Name;
    public static StatusChoice[] All { get; } = [new(OrderStatus.New, "Нове"), new(OrderStatus.Preparing, "Готується"), new(OrderStatus.Served, "Подано"), new(OrderStatus.Paid, "Оплачено"), new(OrderStatus.Cancelled, "Скасовано")];
}
