using System.Collections;
using System.Globalization;
using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;

public sealed class MenuEditorViewModel : ViewModelBase
{
    private string _kind = "Страва";
    private string _name = "";
    private string _category = "";
    private string _price = "";
    private string _portionSize = "1";
    private string _discount = "0";
    private bool _isAvailable = true;
    private IList _selectedComponents = new List<MenuItem>();

    public Guid? Id { get; set; }
    public IReadOnlyList<string> Kinds { get; } = ["Страва", "Напій", "Комбо"];
    public string Name { get => _name; set => Set(ref _name, value); }
    public string Category { get => _category; set => Set(ref _category, value); }
    public string Price { get => _price; set => Set(ref _price, value); }
    public string PortionSize { get => _portionSize; set => Set(ref _portionSize, value); }
    public string Discount { get => _discount; set => Set(ref _discount, value); }
    public bool IsAvailable { get => _isAvailable; set => Set(ref _isAvailable, value); }
    public IList SelectedComponents { get => _selectedComponents; set => Set(ref _selectedComponents, value); }
    public bool IsCombo => Kind == "Комбо";
    public string PortionLabel => Kind switch
    {
        "Страва" => "Маса порції, г",
        "Напій" => "Об’єм порції, мл",
        _ => "Кількість наборів"
    };

    public string Kind
    {
        get => _kind;
        set
        {
            if (!Set(ref _kind, value))
                return;
            Notify(nameof(IsCombo));
            Notify(nameof(PortionLabel));
        }
    }

    public MenuItem CreateItem()
    {
        decimal price = Parse(Price, "ціна");
        decimal portion = Parse(PortionSize, "вихід порції");
        if (Kind == "Комбо")
        {
            var combo = new ComboSet(Name, Category, price, SelectedComponents.Cast<MenuItem>(),
                Parse(Discount, "знижка"), IsAvailable, Id);
            combo.UpdateDetails(Name, Category, price, portion);
            return combo;
        }

        return Kind switch
        {
            "Страва" => new Dish(Name, Category, price, portion, IsAvailable, Id),
            "Напій" => new Drink(Name, Category, price, portion, IsAvailable, Id),
            _ => throw new ArgumentException("Виберіть тип позиції.")
        };
    }

    public static MenuEditorViewModel FromItem(MenuItem item, IReadOnlyList<MenuItem> components)
    {
        var editor = new MenuEditorViewModel
        {
            Id = item.Id,
            Kind = new MenuRow(item).Kind,
            Name = item.Name,
            Category = item.Category,
            Price = item.Price.ToString(CultureInfo.CurrentCulture),
            PortionSize = item.PortionSize.ToString(CultureInfo.CurrentCulture),
            IsAvailable = item.IsAvailable
        };
        if (item is ComboSet combo)
        {
            editor.Discount = combo.DiscountPercent.ToString(CultureInfo.CurrentCulture);
            editor.SelectedComponents = combo.Items
                .Select(part => components.Single(candidate => candidate.Id == part.Id)).ToList();
        }
        return editor;
    }

    private static decimal Parse(string text, string field)
    {
        string normalized = (text ?? "").Trim().Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out decimal value))
            throw new ArgumentException($"Поле «{field}» має містити число без розділювачів тисяч.");
        return value;
    }
}
