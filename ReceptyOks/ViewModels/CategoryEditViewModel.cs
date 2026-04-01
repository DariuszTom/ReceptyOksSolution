namespace ReceptyOks.ViewModels;

public partial class CategoryEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly LocalDatabase _database;
    private CategoryLocal? _editingCategory;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string? selectedIcon;

    [ObservableProperty]
    private bool isEditing;

    [ObservableProperty]
    private string pageTitle = "Nowa kategoria";

    public ObservableCollection<string> AvailableIcons { get; } =
    [
        "breakfast.png",
        "dessert.png",
        "dinner1.png",
        "dinner2.png",
        "muffin.png",
        "soup.png"
    ];

    public CategoryEditViewModel(LocalDatabase database)
    {
        _database = database;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("category", out var categoryObj) && categoryObj is CategoryLocal category)
        {
            _editingCategory = category;
            Name = category.Name;
            Description = category.Description;
            SelectedIcon = category.IconName;
            IsEditing = true;
            PageTitle = "Edytuj kategorię";
        }
        else
        {
            _editingCategory = null;
            Name = string.Empty;
            Description = null;
            SelectedIcon = AvailableIcons.FirstOrDefault();
            IsEditing = false;
            PageTitle = "Nowa kategoria";
        }
    }

    [RelayCommand]
    private void SelectIcon(string iconName)
    {
        SelectedIcon = iconName;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlertAsync("Błąd", "Nazwa kategorii jest wymagana.", "OK");
            return;
        }

        if (_editingCategory is not null)
        {
            _editingCategory.Name = Name;
            _editingCategory.Description = Description;
            _editingCategory.IconName = SelectedIcon;
            await _database.SaveCategoryAsync(_editingCategory);
        }
        else
        {
            var category = new CategoryLocal
            {
                Id = Guid.NewGuid(),
                Name = Name,
                Description = Description,
                IconName = SelectedIcon
            };
            await _database.SaveCategoryAsync(category);
        }

        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private static async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
