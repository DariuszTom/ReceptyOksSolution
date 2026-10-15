using ReceptyOks.Models.HomeSeeker;
using ReceptyOks.Services;
using ReceptyOks.Views;
using System.Globalization;

namespace ReceptyOks.ViewModels;

public enum HomeSeekerSection
{
    Listings,
    Scans,
    Profile
}

/// <summary>Shared HomeSeeker profile, scored listings and scan history.</summary>
public partial class HomeSeekerViewModel(IHomeSeekerService service, ILogger<HomeSeekerViewModel> logger) : ObservableObject
{
    private const int PageSize = 20;
    private const int ScanHistoryCount = 20;

    private static readonly string?[] SortKeys = [null, "price", "firstseen"];
    private static readonly int?[] MinScoreValues = [null, 50, 70, 85];

    private int _currentPage;
    private int _totalPages;

    // Every first-page reload bumps the generation; responses from older generations are dropped
    // so a slow request can never overwrite newer results or pagination state.
    private int _listingsGeneration;
    private int _pendingListingReloads;
    private bool _reloadListingsAfterLoad;

    public IReadOnlyList<string> SortOptions { get; } = ["Ocena AI", "Cena", "Najnowsze"];

    public IReadOnlyList<string> MinScoreOptions { get; } = ["Wszystkie", "Ocena ≥ 50", "Ocena ≥ 70", "Ocena ≥ 85"];

    public ObservableCollection<HomeSeekerListing> Listings { get; } = [];

    public ObservableCollection<HomeSeekerScanRun> Scans { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListingsSection), nameof(IsScansSection), nameof(IsProfileSection))]
    private HomeSeekerSection currentSection = HomeSeekerSection.Listings;

    public bool IsListingsSection => CurrentSection == HomeSeekerSection.Listings;
    public bool IsScansSection => CurrentSection == HomeSeekerSection.Scans;
    public bool IsProfileSection => CurrentSection == HomeSeekerSection.Profile;

    [ObservableProperty]
    private bool isLoading;

    // Separate per RefreshView: setting IsRefreshing on a RefreshView executes its command,
    // so a shared flag would make a scan refresh also reload listings.
    [ObservableProperty]
    private bool isRefreshingListings;

    [ObservableProperty]
    private bool isRefreshingScans;

    [ObservableProperty]
    private bool isLoadingMore;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    [ObservableProperty]
    private string profileStatusText = "Wczytywanie…";

    [ObservableProperty]
    private int totalListings;

    [ObservableProperty]
    private string? selectedSortOption = "Ocena AI";

    [ObservableProperty]
    private string? selectedMinScoreOption = "Wszystkie";

    // Profile form (strings, so partially typed numbers do not get reset by the binding).
    [ObservableProperty] private string city = string.Empty;
    [ObservableProperty] private string? district;
    [ObservableProperty] private bool searchHouses = true;
    [ObservableProperty] private bool searchApartments;
    [ObservableProperty] private string? minPrice;
    [ObservableProperty] private string? maxPrice;
    [ObservableProperty] private string? minArea;
    [ObservableProperty] private string? maxArea;
    /// <summary>0 = no minimum.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinRoomsText))]
    private int minRooms;

    public string MinRoomsText => MinRooms > 0 ? $"{MinRooms}+" : "dowolna";
    [ObservableProperty] private string? extraCriteria;
    [ObservableProperty] private string notificationEmail = string.Empty;
    [ObservableProperty] private bool isActive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
    private string? validationErrors;

    public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationErrors);

    partial void OnSelectedSortOptionChanged(string? value) => ReloadListingsForFilterChange();

    partial void OnSelectedMinScoreOptionChanged(string? value) => ReloadListingsForFilterChange();

    private void ReloadListingsForFilterChange()
    {
        if (IsLoading)
        {
            // LoadAsync may already have requested listings with the previous filters.
            _reloadListingsAfterLoad = true;
            return;
        }

        LoadListingsCommand.Execute(null);
    }

    [RelayCommand]
    private void SelectSection(HomeSeekerSection section) => CurrentSection = section;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;
            _reloadListingsAfterLoad = false;

            var profile = await service.GetProfileAsync(cancellationToken);
            if (profile.IsSuccess && profile.Value is not null)
            {
                ApplyProfile(profile.Value);
            }
            else if (profile.Status == HomeSeekerCallStatus.NotFound)
            {
                ProfileStatusText = "Profil nie jest jeszcze skonfigurowany";
                CurrentSection = HomeSeekerSection.Profile;
                return;
            }
            else
            {
                ProfileStatusText = "Nie udało się wczytać profilu";
                ErrorMessage = profile.Message;
                return;
            }

            await Task.WhenAll(LoadListingsCoreAsync(cancellationToken), LoadScansCoreAsync(cancellationToken));

            // Filters may change again while a replay is in flight; keep going until they settle.
            while (_reloadListingsAfterLoad)
            {
                _reloadListingsAfterLoad = false;
                await LoadListingsCoreAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load HomeSeeker data");
            ErrorMessage = "Nie udało się wczytać danych";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Concurrent so a filter change during a pull-to-refresh is not silently ignored;
    // the generation check keeps only the newest response. IsRefreshingListings is not set to
    // true here: the RefreshView sets it on pull, and setting it programmatically would re-run this command.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadListingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadListingsCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer execution (re-executing the command cancels the previous token).
        }
    }

    [RelayCommand]
    private async Task LoadMoreListingsAsync(CancellationToken cancellationToken)
    {
        if (IsLoadingMore || IsRefreshingListings || IsLoading || _pendingListingReloads > 0 || _currentPage >= _totalPages)
            return;

        var generation = _listingsGeneration;
        try
        {
            IsLoadingMore = true;
            var result = await service.GetListingsAsync(_currentPage + 1, PageSize, CurrentMinScore, CurrentSort, cancellationToken);
            if (generation != _listingsGeneration)
                return; // The list was reloaded meanwhile; this page belongs to the old results.

            if (result.IsSuccess && result.Value is not null)
            {
                foreach (var listing in result.Value.Data)
                    Listings.Add(listing);
                _currentPage = result.Value.PageNumber;
                _totalPages = result.Value.TotalPages;
                TotalListings = result.Value.TotalCount;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task RefreshScansAsync(CancellationToken cancellationToken)
    {
        try
        {
            await LoadScansCoreAsync(cancellationToken);
        }
        finally
        {
            IsRefreshingScans = false;
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync(CancellationToken cancellationToken)
    {
        if (IsSaving)
            return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(City))
            errors.Add("Miasto jest wymagane.");
        if (string.IsNullOrWhiteSpace(NotificationEmail))
            errors.Add("Adres e-mail jest wymagany.");
        if (!SearchHouses && !SearchApartments)
            errors.Add("Wybierz co najmniej jeden typ nieruchomości.");

        var propertyTypes = (SearchHouses ? HomeSeekerPropertyTypes.House : HomeSeekerPropertyTypes.None)
            | (SearchApartments ? HomeSeekerPropertyTypes.Apartment : HomeSeekerPropertyTypes.None);

        var request = new HomeSeekerProfileRequest
        {
            City = City.Trim(),
            District = string.IsNullOrWhiteSpace(District) ? null : District.Trim(),
            PropertyTypes = propertyTypes,
            MinPrice = ParseDecimal(MinPrice, "Cena min", errors),
            MaxPrice = ParseDecimal(MaxPrice, "Cena max", errors),
            MinAreaSqm = ParseDecimal(MinArea, "Metraż min", errors),
            MaxAreaSqm = ParseDecimal(MaxArea, "Metraż max", errors),
            MinRooms = MinRooms > 0 ? MinRooms : null,
            ExtraCriteria = string.IsNullOrWhiteSpace(ExtraCriteria) ? null : ExtraCriteria.Trim(),
            NotificationEmail = NotificationEmail.Trim(),
            IsActive = IsActive
        };

        if (errors.Count > 0)
        {
            ValidationErrors = string.Join('\n', errors);
            return;
        }

        try
        {
            IsSaving = true;
            ValidationErrors = null;
            ErrorMessage = null;

            var result = await service.SaveProfileAsync(request, cancellationToken);
            if (result.IsSuccess && result.Value is not null)
            {
                ApplyProfile(result.Value);
                await SnackBarHelper.ShowInfoSnackbarAsync("Profil zapisany");
                await Task.WhenAll(LoadListingsCoreAsync(cancellationToken), LoadScansCoreAsync(cancellationToken));
            }
            else if (result.Status == HomeSeekerCallStatus.ValidationFailed && result.Errors.Count > 0)
            {
                ValidationErrors = string.Join('\n', result.Errors.SelectMany(e => e.Value));
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private static async Task OpenListingAsync(HomeSeekerListing? listing)
    {
        if (listing is null || !listing.HasHttpUrl)
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

    [RelayCommand]
    private static async Task ShowReportAsync(HomeSeekerScanRun? scan)
    {
        if (scan is null || !scan.HasReport)
            return;

        await Shell.Current.GoToAsync(nameof(HomeSeekerReportPage), new Dictionary<string, object>
        {
            [HomeSeekerReportViewModel.ScanIdQueryKey] = scan.Id,
            [HomeSeekerReportViewModel.TitleQueryKey] = $"Raport {scan.StartedAtLocal:dd.MM.yyyy HH:mm}"
        });
    }

    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");

    private string? CurrentSort => SortKeys[Math.Max(0, IndexOf(SortOptions, SelectedSortOption))];

    private int? CurrentMinScore => MinScoreValues[Math.Max(0, IndexOf(MinScoreOptions, SelectedMinScoreOption))];

    private static int IndexOf(IReadOnlyList<string> options, string? value) =>
        value is null ? -1 : options.ToList().IndexOf(value);

    private async Task LoadListingsCoreAsync(CancellationToken cancellationToken)
    {
        var generation = ++_listingsGeneration;
        _pendingListingReloads++;
        // Old pagination belongs to the previous query; it is restored only when this first page succeeds.
        _currentPage = 0;
        _totalPages = 0;
        try
        {
            var result = await service.GetListingsAsync(1, PageSize, CurrentMinScore, CurrentSort, cancellationToken);
            if (generation != _listingsGeneration)
                return; // A newer reload superseded this one.

            if (result.IsSuccess && result.Value is not null)
            {
                Listings.Clear();
                foreach (var listing in result.Value.Data)
                    Listings.Add(listing);
                _currentPage = result.Value.PageNumber;
                _totalPages = result.Value.TotalPages;
                TotalListings = result.Value.TotalCount;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        finally
        {
            // Whichever reload finishes last (pull-to-refresh, full load, profile save)
            // releases the spinner, so an overlapping caller can't leave it stuck on.
            if (--_pendingListingReloads == 0)
                IsRefreshingListings = false;
        }
    }

    private async Task LoadScansCoreAsync(CancellationToken cancellationToken)
    {
        var result = await service.GetScansAsync(ScanHistoryCount, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            Scans.Clear();
            foreach (var scan in result.Value)
                Scans.Add(scan);
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }

    private void ApplyProfile(HomeSeekerProfile profile)
    {
        City = profile.City;
        District = profile.District;
        SearchHouses = profile.PropertyTypes.HasFlag(HomeSeekerPropertyTypes.House);
        SearchApartments = profile.PropertyTypes.HasFlag(HomeSeekerPropertyTypes.Apartment);
        MinPrice = FormatDecimal(profile.MinPrice);
        MaxPrice = FormatDecimal(profile.MaxPrice);
        MinArea = FormatDecimal(profile.MinAreaSqm);
        MaxArea = FormatDecimal(profile.MaxAreaSqm);
        MinRooms = profile.MinRooms ?? 0;
        ExtraCriteria = profile.ExtraCriteria;
        NotificationEmail = profile.NotificationEmail;
        IsActive = profile.IsActive;

        var location = string.IsNullOrWhiteSpace(profile.District) ? profile.City : $"{profile.City}, {profile.District}";
        var state = profile.IsActive ? "aktywny" : "wstrzymany";
        var lastScan = profile.LastScannedAtLocal is { } scanned
            ? $"ostatni skan {scanned:dd.MM.yyyy HH:mm}"
            : "jeszcze nie skanowano";
        ProfileStatusText = $"{location} · {state} · {lastScan}";
    }

    private static string? FormatDecimal(decimal? value) =>
        value?.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>
    /// Parses user input such as "1 250 000", "450000" or "120,5"/"120.5" independently of the device culture;
    /// returns null for empty input. Spaces are the only accepted grouping; both ',' and '.' mean the decimal
    /// separator, so "120,5" is never read as 1205.
    /// </summary>
    public static decimal? ParseDecimal(string? text, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Replace(" ", string.Empty).Replace("\u00A0", string.Empty).Replace("\u202F", string.Empty)
            .Replace(',', '.');

        if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            if (value < 0)
            {
                errors.Add($"{fieldName}: wartość nie może być ujemna.");
                return null;
            }
            return value;
        }

        errors.Add($"{fieldName}: nieprawidłowa liczba.");
        return null;
    }
}
