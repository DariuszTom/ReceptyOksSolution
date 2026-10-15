using System.ComponentModel;
using ReceptyOks.ViewModels;
using UraniumUI.Material.Controls;

namespace ReceptyOks.Views;

public partial class HomeSeekerPage : ContentPage
{
    private readonly HomeSeekerViewModel _viewModel;
    private bool _initialized;
    private bool _componentReady;

    public HomeSeekerPage(HomeSeekerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        BindingContext = _viewModel;
        SectionTabs.SelectedTab = TabFor(_viewModel.CurrentSection);
        _componentReady = true;
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
        // TabView raises this while XAML is loading (inside InitializeComponent).
        if (!_componentReady)
            return;

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
