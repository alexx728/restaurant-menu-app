namespace Restaurant.Domain.Models;

public sealed class Drink : MenuItem
{
    public Drink(string name, string category, decimal price, decimal volumeMilliliters,
        bool isAvailable = true, Guid? id = null)
        : base(name, category, price, volumeMilliliters, isAvailable, id)
    {
    }

    public override string PortionUnit => "мл";

    public override decimal CalculatePrice() => Price;
}
