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

    private void OnTimeSlotTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view) return;

        var hourSlot = view.BindingContext as HourSlot;
        if (hourSlot is null) return;

        var parent = view.Parent;
        while (parent is not null)
        {
            if (parent.BindingContext is DayPlanItem dayPlan)
            {
                _viewModel.OnTimeSlotTapped(dayPlan, hourSlot.Hour);
                return;
            }
            parent = parent.Parent;
        }
    }

    private void OnRemoveMealClicked(object? sender, EventArgs e)
    {
        if (sender is not View view) return;

        var hourSlot = view.BindingContext as HourSlot;
        if (hourSlot?.MealRef is null) return;

        _viewModel.RemoveMealCommand.Execute(hourSlot.MealRef);
    }

    private void OnMealBlockTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not View view) return;

        var hourSlot = view.BindingContext as HourSlot;
        if (hourSlot?.MealRef is null) return;

        _viewModel.GoToRecipeDetailCommand.Execute(hourSlot.MealRef);
    }

    private void OnToggleTimelineClicked(object? sender, EventArgs e)
    {
        if (sender is not View view) return;

        if (view.BindingContext is DayPlanItem dayPlan)
        {
            dayPlan.IsExpanded = !dayPlan.IsExpanded;
        }
    }
}
