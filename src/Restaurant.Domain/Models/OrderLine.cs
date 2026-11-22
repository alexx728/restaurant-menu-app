namespace Restaurant.Domain.Models;
public sealed class OrderLine
{
    public Guid Id { get; }
    public Guid MenuItemId { get; }
    public string Name { get; }
    public string Category { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; }
    public decimal Total => checked(UnitPrice * Quantity);

    internal OrderLine(Guid id, Guid menuItemId, string name, string category, decimal unitPrice, int quantity)
    {
        if (id == Guid.Empty || menuItemId == Guid.Empty)
            throw new ArgumentException("Порожній ідентифікатор позиції.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Назва й категорія обов'язкові.");
        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (quantity is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Кількість: від 1 до 50.");
        Id = id;
        MenuItemId = menuItemId;
        Name = name.Trim();
        Category = category.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
        _ = Total;
    }

    internal OrderLine WithQuantity(int quantity) => new(Id, MenuItemId, Name, Category, UnitPrice, quantity);
}
