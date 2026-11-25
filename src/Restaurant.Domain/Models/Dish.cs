namespace Restaurant.Domain.Models;

public sealed class Dish : MenuItem
{
    public Dish(string name, string category, decimal price, decimal weightGrams,
        bool isAvailable = true, Guid? id = null)
        : base(name, category, price, weightGrams, isAvailable, id)
    {
    }

    public override string PortionUnit => "г";

    public override decimal CalculatePrice() => Price;
}
