using ReceptyOks.BlazorComponents.Services;
using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class AppStatusView : ContentPage
{
    private readonly AppStatusViewModel _viewModel;
    private readonly MemoryChartState _chartState;

    public AppStatusView(AppStatusViewModel viewModel, MemoryChartState chartState)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _chartState = chartState;
        BindingContext = viewModel;

        // Subscribe to memory data changes
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Auto-check health when page appears
        _chartState.SetLoading(true);
        await _viewModel.CheckHealthAsync();

        UpdateChartState();
        _chartState.SetLoading(false);

        UpdateChartState();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppStatusViewModel.MemoryHistory) ||
            e.PropertyName == nameof(AppStatusViewModel.CurrentMemoryMB))
        {
            MainThread.BeginInvokeOnMainThread(UpdateChartState);
        }
        else if (e.PropertyName == nameof(AppStatusViewModel.IsLoading))
        {
            MainThread.BeginInvokeOnMainThread(() => _chartState.SetLoading(_viewModel.IsLoading));
        }
    }

    private void UpdateChartState()
    {
        try
        {
            var memoryData = _viewModel.MemoryHistory
                .Select(m => new MemoryChartState.MemoryDataPoint
                {
                    Timestamp = m.Timestamp,
                    MemoryMB = m.MemoryMB
                })
                .ToList(); // Materialize to avoid deferred execution issues

            _chartState.SetData(memoryData);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating chart state: {ex.Message}");
        }
    }
}