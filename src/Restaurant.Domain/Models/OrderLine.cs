namespace Restaurant.Domain.Models;

public sealed class OrderLine
{
    public Guid Id { get; }
    public Guid MenuItemId { get; }
    public string Name { get; }
    public string Category { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }
    public decimal Total { get; }

    internal OrderLine(Guid id, Guid menuItemId, string name, string category, decimal unitPrice, int quantity)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Ідентифікатор рядка не може бути порожнім.", nameof(id));
        if (menuItemId == Guid.Empty)
            throw new ArgumentException("Ідентифікатор позиції меню не може бути порожнім.", nameof(menuItemId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Потрібно вказати назву позиції.", nameof(name));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Потрібно вказати категорію позиції.", nameof(category));
        if (unitPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Ціна рядка не може бути від'ємною.");
        if (quantity is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Кількість має бути від 1 до 50.");

        Total = checked(unitPrice * quantity);
        Id = id;
        MenuItemId = menuItemId;
        Name = name.Trim();
        Category = category.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal OrderLine WithQuantity(int quantity) =>
        new(Id, MenuItemId, Name, Category, UnitPrice, quantity);
}
