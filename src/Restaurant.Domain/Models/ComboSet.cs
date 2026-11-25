namespace Restaurant.Domain.Models;

public sealed class ComboSet : MenuItem, IDiscountable
{
    public IReadOnlyList<MenuItem> Items { get; }
    public decimal DiscountPercent { get; private set; }
    public override string PortionUnit => "набір";

    public ComboSet(string name, string category, decimal price, IEnumerable<MenuItem> items,
        decimal discountPercent = 0m, bool isAvailable = true, Guid? id = null)
        : base(name, category, price, 1m, isAvailable, id)
    {
        ArgumentNullException.ThrowIfNull(items);
        var components = new List<MenuItem>();
        foreach (MenuItem item in items)
        {
            if (item is not Dish and not Drink)
                throw new ArgumentException("До комбо можна додавати лише страви й напої.", nameof(items));
            components.Add(item);
        }

        if (components.Count < 2)
            throw new ArgumentException("Комбо має містити щонайменше дві позиції.", nameof(items));

        SetDiscount(discountPercent);
        Items = components.AsReadOnly();
    }

    public void SetDiscount(decimal discountPercent)
    {
        if (discountPercent is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Знижка має бути від 0 до 100 відсотків.");

        DiscountPercent = discountPercent;
    }

    public override decimal CalculatePrice()
    {
        decimal price = Price * (1m - DiscountPercent / 100m);
        return decimal.Round(price, 2, MidpointRounding.AwayFromZero);
    }
}
