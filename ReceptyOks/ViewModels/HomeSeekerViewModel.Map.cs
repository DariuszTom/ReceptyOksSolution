using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.Services;
using System.Globalization;

namespace ReceptyOks.ViewModels;

/// <summary>"Mapa" tab: best scored listings as pins (same filters as the list) and "our location".</summary>
public partial class HomeSeekerViewModel
{
    public const int MapListingLimit = 50;

    /// <summary>"All" for the map endpoint (the server applies its own safety cap).</summary>
    public const int MapAllListings = 0;

    private static readonly int[] MapLimitValues = [50, 100, 200, 300, MapAllListings];

    public IReadOnlyList<string> MapLimitOptions { get; } = ["50", "100", "200", "300", "Wszystkie"];

    [ObservableProperty]
    private string? selectedMapLimitOption = "50";

    public int CurrentMapLimit => MapLimitValues[Math.Max(0, IndexOf(MapLimitOptions, SelectedMapLimitOption))];

    partial void OnSelectedMapLimitOptionChanged(string? value) => ReloadMapIfLoaded();

    private HomeSeekerProfile? _profile;
    private int _mapGeneration;
    private bool _mapLoaded;

    /// <summary>Raised after <see cref="MapPins"/> or the home location change (the view redraws the layers).</summary>
    public event EventHandler? MapDataChanged;

    public IReadOnlyList<MapPin> MapPins { get; private set; } = [];

    [ObservableProperty]
    private bool isMapLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMapListing))]
    private HomeSeekerMapListing? selectedMapListing;

    public bool HasSelectedMapListing => SelectedMapListing is not null;

    [ObservableProperty]
    private string mapStatusText = string.Empty;

    public double? HomeLatitude => _profile?.HomeLatitude;

    public double? HomeLongitude => _profile?.HomeLongitude;

    public bool HasHome => _profile?.HasHome == true;

    public string DistanceReferenceText => HasHome
        ? $"Odległości liczone od naszej lokalizacji{(string.IsNullOrWhiteSpace(_profile?.HomeLabel) ? string.Empty : $" ({_profile!.HomeLabel})")}"
        : "Odległości liczone od centrum miasta · przytrzymaj mapę, aby ustawić naszą lokalizację";

    partial void OnCurrentSectionChanged(HomeSeekerSection value)
    {
        if (value == HomeSeekerSection.Map && !_mapLoaded && _profile is not null)
            LoadMapCommand.Execute(null);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadMapAsync(CancellationToken cancellationToken)
    {
        var generation = ++_mapGeneration;
        try
        {
            IsMapLoading = true;
            var result = await service.GetMapListingsAsync(CurrentMapLimit, CurrentQuery, cancellationToken);
            if (generation != _mapGeneration)
                return;

            if (result.IsSuccess && result.Value is not null)
            {
                _mapLoaded = true;
                MapPins = MapPinLayout.Spread(result.Value);
                if (SelectedMapListing is { } selected && MapPins.All(p => p.Listing.Id != selected.Id))
                    SelectedMapListing = null;
                MapStatusText = MapPins.Count == 0
                    ? "Brak ocenionych ofert z lokalizacją dla wybranych filtrów"
                    : CurrentMapLimit == MapAllListings
                        ? $"Wszystkie oferty ({MapPins.Count})"
                        : $"{MapPins.Count} najlepszych ofert";
                MapDataChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer execution.
        }
        finally
        {
            if (generation == _mapGeneration)
                IsMapLoading = false;
        }
    }

    [RelayCommand]
    private void SelectMapListing(HomeSeekerMapListing? listing) => SelectedMapListing = listing;

    [RelayCommand]
    private async Task OpenMapListingAsync()
    {
        if (SelectedMapListing is not { HasHttpUrl: true } listing)
            return;

        try
        {
            await Browser.Default.OpenAsync(new Uri(listing.Url), BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception)
        {
            await SnackBarHelper.ShowErrorSnackbarAsync("Nie udało się otworzyć ogłoszenia");
        }
    }

    /// <summary>Saves "our location"; distances are recomputed by the server and the list/map reloaded.</summary>
    public async Task<bool> SetHomeAsync(double latitude, double longitude, string? label, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return false;

        return await SaveHomeAsync(Math.Round(latitude, 6), Math.Round(longitude, 6), label, cancellationToken);
    }

    [RelayCommand]
    private async Task ClearHomeAsync(CancellationToken cancellationToken) =>
        await SaveHomeAsync(null, null, null, cancellationToken);

    [RelayCommand]
    private async Task UseGpsLocationAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await SnackBarHelper.ShowErrorSnackbarAsync("Brak uprawnienia do lokalizacji");
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)), cancellationToken);
            if (location is null)
            {
                await SnackBarHelper.ShowErrorSnackbarAsync("Nie udało się ustalić lokalizacji");
                return;
            }

            await SetHomeAsync(location.Latitude, location.Longitude, "GPS", cancellationToken);
        }
        catch (Exception ex) when (ex is FeatureNotSupportedException or FeatureNotEnabledException or PermissionException)
        {
            await SnackBarHelper.ShowErrorSnackbarAsync("Lokalizacja jest niedostępna na tym urządzeniu");
        }
    }

    private async Task<bool> SaveHomeAsync(double? latitude, double? longitude, string? label, CancellationToken cancellationToken)
    {
        if (_profile is null || IsSaving)
            return false;

        var request = _profile.ToRequest();
        request.HomeLatitude = latitude;
        request.HomeLongitude = longitude;
        request.HomeLabel = latitude is null ? null : label;

        try
        {
            IsSaving = true;
            ErrorMessage = null;

            var result = await service.SaveProfileAsync(request, cancellationToken);
            if (result.IsSuccess && result.Value is not null)
            {
                ApplyProfile(result.Value);
                await SnackBarHelper.ShowInfoSnackbarAsync(latitude is null
                    ? "Usunięto naszą lokalizację – odległości od centrum miasta"
                    : "Zapisano naszą lokalizację");
                await Task.WhenAll(LoadListingsCoreAsync(cancellationToken), LoadMapAsync(cancellationToken));
                return true;
            }

            ErrorMessage = result.Status == HomeSeekerCallStatus.ValidationFailed && result.Errors.Count > 0
                ? string.Join('\n', result.Errors.SelectMany(e => e.Value))
                : result.Message;
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void ReloadMapIfLoaded()
    {
        if (_mapLoaded)
            LoadMapCommand.Execute(null);
    }

    private void OnHomeChanged()
    {
        OnPropertyChanged(nameof(HomeLatitude));
        OnPropertyChanged(nameof(HomeLongitude));
        OnPropertyChanged(nameof(HasHome));
        OnPropertyChanged(nameof(DistanceReferenceText));
        MapDataChanged?.Invoke(this, EventArgs.Empty);
    }

    public static string FormatCoordinates(double latitude, double longitude) =>
        string.Create(CultureInfo.InvariantCulture, $"{latitude:0.00000}, {longitude:0.00000}");
}
