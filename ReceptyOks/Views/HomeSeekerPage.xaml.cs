using System.ComponentModel;
using ReceptyOks.ViewModels;
using UraniumUI.Material.Controls;

namespace ReceptyOks.Views;

public partial class HomeSeekerPage : ContentPage
{
    private readonly HomeSeekerViewModel _viewModel;
    private bool _initialized;

    public HomeSeekerPage(HomeSeekerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(HomeSeekerViewModel.CurrentSection))
            return;

        var target = TabFor(_viewModel.CurrentSection);
        if (!ReferenceEquals(SectionTabs.SelectedTab, target))
            MainThread.BeginInvokeOnMainThread(() => SectionTabs.SelectedTab = target);
    }

    private void OnSelectedTabChanged(object? sender, TabItem? tab)
    {
        var section = tab == ProfileTab ? HomeSeekerSection.Profile
            : tab == ScansTab ? HomeSeekerSection.Scans
            : HomeSeekerSection.Listings;

        if (_viewModel.CurrentSection != section)
            _viewModel.CurrentSection = section;
    }

    private TabItem TabFor(HomeSeekerSection section) => section switch
    {
        HomeSeekerSection.Profile => ProfileTab,
        HomeSeekerSection.Scans => ScansTab,
        _ => ListingsTab,
    };

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Coming back from the report page should not reset scroll position / paging.
        if (_initialized)
            return;

        _initialized = true;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
