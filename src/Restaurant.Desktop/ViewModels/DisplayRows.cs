using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;

public sealed record MenuRow(MenuItem Item)
{
    public string Name => Item.Name;
    public string Category => Item.Category;
    public decimal Price => Item.Price;
    public decimal FinalPrice => Item.CalculatePrice();
    public bool IsAvailable => Item.IsAvailable;
    public string Portion => $"{Item.PortionSize:0.##} {Item.PortionUnit}";
    public string Kind => Item switch
    {
        Dish => "Страва",
        Drink => "Напій",
        ComboSet => "Комбо",
        _ => throw new InvalidOperationException("Невідомий тип позиції.")
    };
}

public sealed record OrderRow(Order Item)
{
    public int Table => Item.TableNumber;
    public DateTimeOffset CreatedAt => Item.CreatedAt.ToLocalTime();
    public decimal Total => Item.Total;
    public string Status => StatusChoice.All.Single(choice => choice.Value == Item.Status).Name;
}

public sealed record StatusChoice(OrderStatus? Value, string Name)
{
    public static IReadOnlyList<StatusChoice> All { get; } = Array.AsReadOnly<StatusChoice>(
    [
        new(OrderStatus.New, "Нове"),
        new(OrderStatus.Preparing, "Готується"),
        new(OrderStatus.Served, "Подано"),
        new(OrderStatus.Paid, "Оплачено"),
        new(OrderStatus.Cancelled, "Скасовано")
    ]);

    public override string ToString() => Name;
}
