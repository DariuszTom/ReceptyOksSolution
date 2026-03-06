using ReceptyOks.Shared;
using System.Windows.Input;

namespace ReceptyOks.Controls;

/// <summary>
/// A reusable control for adding shopping list items with name, quantity, and unit fields.
/// </summary>
public partial class Ingriedent : ContentView
{
    #region Bindable Properties

    public static readonly BindableProperty ItemNameProperty = BindableProperty.Create(
        nameof(ItemName),
        typeof(string),
        typeof(Ingriedent),
        string.Empty,
        BindingMode.TwoWay);

    public string ItemName
    {
        get => (string)GetValue(ItemNameProperty);
        set => SetValue(ItemNameProperty, value);
    }

    public static readonly BindableProperty ItemNameTitleProperty = BindableProperty.Create(
        nameof(ItemNameTitle),
        typeof(string),
        typeof(Ingriedent),
        "Nazwa produktu");

    public string ItemNameTitle
    {
        get => (string)GetValue(ItemNameTitleProperty);
        set => SetValue(ItemNameTitleProperty, value);
    }

    public static readonly BindableProperty ItemQuantityProperty = BindableProperty.Create(
    nameof(ItemQuantity),
        typeof(string),
        typeof(Ingriedent),
      string.Empty,
        BindingMode.TwoWay);

    public string ItemQuantity
    {
        get => (string)GetValue(ItemQuantityProperty);
        set => SetValue(ItemQuantityProperty, value);
    }

    public static readonly BindableProperty QuantityTitleProperty = BindableProperty.Create(
        nameof(QuantityTitle),
        typeof(string),
        typeof(Ingriedent),
        "Ilość");

    public string QuantityTitle
    {
        get => (string)GetValue(QuantityTitleProperty);
        set => SetValue(QuantityTitleProperty, value);
    }

    public static readonly BindableProperty UnitTitleProperty = BindableProperty.Create(
            nameof(UnitTitle),
            typeof(string),
            typeof(Ingriedent), "Jednostka");

    public string UnitTitle
    {
        get => (string)GetValue(UnitTitleProperty);
        set => SetValue(UnitTitleProperty, value);
    }

    public static readonly BindableProperty SelectedUnitProperty = BindableProperty.Create(
        nameof(SelectedUnit),
        typeof(Jednostki),
        typeof(Ingriedent),
        Jednostki.Brak,
        BindingMode.TwoWay);

    public Jednostki SelectedUnit
    {
        get => (Jednostki)GetValue(SelectedUnitProperty);
        set => SetValue(SelectedUnitProperty, value);
    }

    public static readonly BindableProperty AvailableUnitsProperty = BindableProperty.Create(
        nameof(AvailableUnits),
        typeof(IReadOnlyList<Jednostki>),
        typeof(Ingriedent),
        Enum.GetValues(typeof(Jednostki)).Cast<Jednostki>().ToList() as IReadOnlyList<Jednostki>);

    public IReadOnlyList<Jednostki> AvailableUnits
    {
        get => (IReadOnlyList<Jednostki>)GetValue(AvailableUnitsProperty);
        set => SetValue(AvailableUnitsProperty, value);
    }

    public static readonly BindableProperty AddCommandProperty = BindableProperty.Create(
        nameof(AddCommand),
        typeof(ICommand),
        typeof(Ingriedent));

    public ICommand AddCommand
    {
        get => (ICommand)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public static readonly BindableProperty ErrorMessageProperty = BindableProperty.Create(
        nameof(ErrorMessage),
        typeof(string),
        typeof(Ingriedent));

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    /// <summary>
    /// Collection of ingredient name suggestions for autocomplete.
    /// </summary>
    public static readonly BindableProperty IngredientSuggestionsProperty = BindableProperty.Create(
        nameof(IngredientSuggestions),
        typeof(IEnumerable<string>),
        typeof(Ingriedent),
        Enumerable.Empty<string>());

    public IEnumerable<string> IngredientSuggestions
    {
        get => (IEnumerable<string>)GetValue(IngredientSuggestionsProperty);
        set => SetValue(IngredientSuggestionsProperty, value);
    }

    #endregion

    public Ingriedent()
    {
        InitializeComponent();
    }
}