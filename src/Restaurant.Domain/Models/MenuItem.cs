namespace Restaurant.Domain.Models;
public abstract class MenuItem
{
    public Guid Id { get; }
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public decimal PortionSize { get; private set; }
    public bool IsAvailable { get; private set; }
    public abstract string PortionUnit { get; }

    protected MenuItem(string name, string category, decimal price, decimal portionSize, bool isAvailable = true, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        if (Id == Guid.Empty)
            throw new ArgumentException("Порожній ідентифікатор.", nameof(id));
        UpdateDetails(name, category, price, portionSize);
        IsAvailable = isAvailable;
    }

    public void UpdateDetails(string name, string category, decimal price, decimal portionSize)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва позиції не може бути порожньою.", nameof(name));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Категорія не може бути порожньою.", nameof(category));
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Ціна має бути більшою за 0.");
        if (portionSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(portionSize), "Вихід порції має бути більшим за 0.");
        Name = name.Trim();
        Category = category.Trim();
        Price = price;
        PortionSize = portionSize;
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;
    public abstract decimal CalculatePrice();
    public override string ToString() => $"{Name} ({Category}), {PortionSize} {PortionUnit}, {CalculatePrice():0.00} грн";
}
