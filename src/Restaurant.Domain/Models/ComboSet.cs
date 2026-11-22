namespace Restaurant.Domain.Models;
public sealed class ComboSet : MenuItem, IDiscountable
{
    private const decimal FullPercent = 100m;
    private const int MinimumItems = 2;
    public IReadOnlyList<MenuItem> Items { get; }
    public decimal DiscountPercent { get; private set; }
    public override string PortionUnit => "набір";

    public ComboSet(string name, string category, decimal price, IEnumerable<MenuItem> items, decimal discountPercent = 0m, bool isAvailable = true, Guid? id = null) : base(name, category, price, 1m, isAvailable, id)
    {
        ArgumentNullException.ThrowIfNull(items);
        MenuItem[] snapshot = items.ToArray();
        if (snapshot.Length < MinimumItems)
            throw new ArgumentException("Набір має містити щонайменше дві позиції.", nameof(items));
        if (snapshot.Any(item => item is not Dish and not Drink))
            throw new ArgumentException("Складниками можуть бути лише страви й напої.", nameof(items));
        Items = Array.AsReadOnly(snapshot);
        SetDiscount(discountPercent);
    }

    public void SetDiscount(decimal discountPercent)
    {
        if (discountPercent < 0m || discountPercent > FullPercent)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Знижка має бути в межах від 0 до 100 відсотків.");
        DiscountPercent = discountPercent;
    }

    public override decimal CalculatePrice() => decimal.Round(Price * (1m - DiscountPercent / FullPercent), 2, MidpointRounding.AwayFromZero);
}
