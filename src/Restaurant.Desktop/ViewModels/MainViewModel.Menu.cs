using Restaurant.Domain.Models;

namespace Restaurant.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    private MenuEditorViewModel _menuEditor = new();
    private MenuRow? _selectedMenu;
    private string _searchText = "";
    private string _categoryFilter = "";
    private bool _priceDescending;

    public MenuEditorViewModel MenuEditor { get => _menuEditor; private set => Set(ref _menuEditor, value); }
    public string SearchText { get => _searchText; set => Set(ref _searchText, value); }
    public string CategoryFilter { get => _categoryFilter; set => Set(ref _categoryFilter, value); }
    public bool PriceDescending { get => _priceDescending; set => Set(ref _priceDescending, value); }
    public IReadOnlyList<MenuItem> Components { get; private set; } = [];
    public string MenuEditorTitle => MenuEditor.Id.HasValue ? "Редагувати позицію" : "Нова позиція";

    public MenuRow? SelectedMenu
    {
        get => _selectedMenu;
        set
        {
            Set(ref _selectedMenu, value);
            MenuEditor = value is null ? new MenuEditorViewModel() : MenuEditorViewModel.FromItem(value.Item, Components);
            Notify(nameof(MenuEditorTitle));
            RefreshCommands();
        }
    }

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
            Message = "Заповніть назву, категорію, ціну та вихід порції.";
        });
        SaveMenuCommand = Command(SaveMenu);
        DeleteMenuCommand = Command(DeleteMenuAsync, () => SelectedMenu is not null);
    }

    private void SaveMenu()
    {
        MenuItem item = MenuEditor.CreateItem();
        _service.SaveMenu(item);
        Changed("Позицію збережено. Запишіть зміни у JSON, щоб зберегти їх після закриття.");
        SelectedMenu = MenuRows.FirstOrDefault(row => row.Item.Id == item.Id);
        MenuEditor = MenuEditorViewModel.FromItem(item, Components);
        Notify(nameof(MenuEditorTitle));
    }

    private async Task DeleteMenuAsync()
    {
        MenuRow row = SelectedMenu ?? throw new InvalidOperationException("Виберіть позицію меню.");
        if (await ConfirmAsync($"Видалити «{row.Name}» з меню?"))
        {
            _service.DeleteMenu(row.Item.Id);
            Changed("Позицію вилучено з меню.");
        }
    }
}
