using System.Collections;
using System.Globalization;
using Restaurant.Domain.Models;
using MenuItem = Restaurant.Domain.Models.MenuItem;

namespace Restaurant.Desktop.ViewModels;
public sealed class MenuEditorViewModel
{
    public Guid? Id { get; set; }
    public string Kind { get; set; } = "Страва";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Price { get; set; } = "";
    public string PortionSize { get; set; } = "1";
    public string Discount { get; set; } = "0";
    public bool IsAvailable { get; set; } = true;
    public IList SelectedComponents { get; set; } = new List<MenuItem>();
    public string[] Kinds { get; } = ["Страва", "Напій", "Комбо"];

    public MenuItem CreateItem()
    {
        decimal price = Parse(Price), portion = Parse(PortionSize);
        MenuItem item = Kind switch
        {
            "Страва" => new Dish(Name, Category, price, portion, IsAvailable, Id),
            "Напій" => new Drink(Name, Category, price, portion, IsAvailable, Id),
            "Комбо" => new ComboSet(Name, Category, price, SelectedComponents.Cast<MenuItem>(), Parse(Discount), IsAvailable, Id),
            _ => throw new ArgumentException("Виберіть тип позиції.")};
        if (item is ComboSet)
            item.UpdateDetails(Name, Category, price, portion);
        return item;
    }

    public static MenuEditorViewModel FromItem(MenuItem item, IReadOnlyList<MenuItem> components) => new()
    {
        Id = item.Id,
        Kind = new MenuRow(item).Kind,
        Name = item.Name,
        Category = item.Category,
        Price = item.Price.ToString(CultureInfo.CurrentCulture),
        PortionSize = item.PortionSize.ToString(CultureInfo.CurrentCulture),
        IsAvailable = item.IsAvailable,
        Discount = (item is ComboSet c ? c.DiscountPercent : 0m).ToString(CultureInfo.CurrentCulture),
        SelectedComponents = item is ComboSet combo ? combo.Items.Select(x => components.First(y => y.Id == x.Id)).ToList() : new List<MenuItem>()
    };
    private static decimal Parse(string text) => decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal number) ? number : throw new ArgumentException("Введіть число без розділювачів тисяч.");
}
