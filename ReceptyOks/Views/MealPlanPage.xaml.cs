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

    private void OnDateSlotTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view) return;

        if (view.BindingContext is DateSlot dateSlot)
        {
            _viewModel.OnDateSlotTapped(dateSlot);
        }
    }

    private void OnAddToDateSlotTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view) return;

        // Walk up to find the DateSlot context
        var current = view as Element;
        while (current is not null)
        {
            if (current.BindingContext is DateSlot dateSlot)
            {
                _viewModel.OnDateSlotTapped(dateSlot);
                return;
            }
            current = current.Parent;
        }
    }

    private void OnRemoveChipClicked(object? sender, EventArgs e)
    {
        if (sender is not View view) return;

        if (view.BindingContext is MealItem meal)
        {
            _viewModel.RemoveMealCommand.Execute(meal);
        }
    }

    private void OnMealChipTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view) return;

        if (view.BindingContext is MealItem meal)
        {
            _viewModel.GoToRecipeDetailCommand.Execute(meal);
        }
    }

    private void OnToggleWeekTimelineClicked(object? sender, EventArgs e)
    {
        _viewModel.IsWeekExpanded = !_viewModel.IsWeekExpanded;
    }
}
