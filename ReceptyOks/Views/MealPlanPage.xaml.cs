using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class MealPlanPage : ContentPage
{
    private readonly MealPlanViewModel _viewModel;

    public MealPlanPage(MealPlanViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataCommand.ExecuteAsync(null);
    }

    private void OnAddMealClicked(object? sender, EventArgs e)
    {
        if (sender is ImageButton button && button.BindingContext is DayPlanItem dayPlan)
        {
            _viewModel.OpenRecipePickerCommand.Execute(dayPlan);
        }
    }
}
