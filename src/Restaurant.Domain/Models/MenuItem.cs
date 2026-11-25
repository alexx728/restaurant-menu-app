namespace Restaurant.Domain.Models;

public abstract class MenuItem
{
    public Guid Id { get; }
    public string Name { get; private set; } = "";
    public string Category { get; private set; } = "";
    public decimal Price { get; private set; }
    public decimal PortionSize { get; private set; }
    public bool IsAvailable { get; private set; }
    public abstract string PortionUnit { get; }

    protected MenuItem(string name, string category, decimal price, decimal portionSize,
        bool isAvailable = true, Guid? id = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Ідентифікатор позиції не може бути порожнім.", nameof(id));

        UpdateDetails(name, category, price, portionSize);
        Id = id ?? Guid.NewGuid();
        IsAvailable = isAvailable;
    }

    public void UpdateDetails(string name, string category, decimal price, decimal portionSize)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Потрібно вказати назву позиції.", nameof(name));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Потрібно вказати категорію позиції.", nameof(category));
        if (price <= 0m)
            throw new ArgumentOutOfRangeException(nameof(price), "Ціна має бути більшою за нуль.");
        if (portionSize <= 0m)
            throw new ArgumentOutOfRangeException(nameof(portionSize), "Вихід порції має бути більшим за нуль.");

        Name = name.Trim();
        Category = category.Trim();
        Price = price;
        PortionSize = portionSize;
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;

    public abstract decimal CalculatePrice();

    public override string ToString() =>
        $"{Name} ({Category}), {PortionSize} {PortionUnit}, {CalculatePrice():0.00} грн";
}
