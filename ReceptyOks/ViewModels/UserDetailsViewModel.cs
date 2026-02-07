using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceptyOks.Services;
using ReceptyOks.Shared.Models;

namespace ReceptyOks.ViewModels;

/// <summary>
/// ViewModel for the UserDetailsPage that handles loading and saving user information.
/// </summary>
public partial class UserDetailsViewModel : ObservableObject
{
    private readonly UserService _userService;

    [ObservableProperty]
    private User userDetails = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isSaving;

    public UserDetailsViewModel()
    {
        _userService = UserService.Instance.Value;
    }

    /// <summary>
    /// Loads user details from storage. Called when the page appears.
    /// </summary>
    [RelayCommand]
    private async Task LoadUserDetailsAsync(CancellationToken cancellationToken)
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            var user = await _userService.GetUserAsync().ConfigureAwait(false);
            UserDetails = user ?? new User();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves the current user details to storage.
    /// </summary>
    [RelayCommand]
    private async Task SaveUserDetailsAsync(CancellationToken cancellationToken)
    {
        if (IsSaving)
        {
            return;
        }

        try
        {
            IsSaving = true;
            await _userService.SetUserAsync(UserDetails).ConfigureAwait(false);
            await Snackbar.Make("Dane użytkownika zostały zapisane").Show();
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private static async Task GoBackAsync() => await Shell.Current.GoToAsync("..");
}
