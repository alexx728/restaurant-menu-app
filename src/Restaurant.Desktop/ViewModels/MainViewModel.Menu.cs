using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;
public sealed partial class MainViewModel
{
    private MenuEditorViewModel _menuEditor = new();
    private MenuRow? _selectedMenu;
    public MenuEditorViewModel MenuEditor { get => _menuEditor; private set => Set(ref _menuEditor, value); }

    public MenuRow? SelectedMenu
    {
        get => _selectedMenu;
        set
        {
            Set(ref _selectedMenu, value);
            if (value is not null)
                MenuEditor = MenuEditorViewModel.FromItem(value.Item, Components);
        }
    }

    public IReadOnlyList<MenuItem> Components { get; private set; } = [];
    public string SearchText { get; set; } = "";
    public string CategoryFilter { get; set; } = "";
    public bool PriceDescending { get; set; }
    public AsyncCommand FilterCommand { get; private set; } = null!;
    public AsyncCommand NewMenuCommand { get; private set; } = null!;
    public AsyncCommand SaveMenuCommand { get; private set; } = null!;
    public AsyncCommand DeleteMenuCommand { get; private set; } = null!;

    private void InitializeMenuCommands()
    {
        FilterCommand = Command(Refresh);
        NewMenuCommand = Command(() =>
        {
            SelectedMenu = null;
            MenuEditor = new();
            Message = "Введіть дані нової позиції.";
        });
        SaveMenuCommand = Command(() =>
        {
            var item = MenuEditor.CreateItem();
            _service.SaveMenu(item);
            Changed("Позицію меню збережено в пам'яті. Для запису у файл натисніть «Зберегти JSON».");
            SelectedMenu = MenuRows.FirstOrDefault(x => x.Item.Id == item.Id);
            MenuEditor = MenuEditorViewModel.FromItem(item, Components);
        });
        DeleteMenuCommand = Command(async () =>
        {
            var row = SelectedMenu ?? throw new InvalidOperationException("Виберіть позицію меню.");
            if (!await _confirm($"Видалити «{row.Name}» з меню?"))
                return;
            _service.DeleteMenu(row.Item.Id);
            MenuEditor = new();
            Changed("Позицію видалено з меню.");
        });
    }
}
