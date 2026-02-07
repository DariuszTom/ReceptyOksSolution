using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using ReceptyOks.Shared;

namespace ReceptyOks.Services
{
    internal static class SnackBarHelper
    {
        /// <summary>
        /// Shows an error snackbar with the specified message.
        /// </summary>
        internal static async Task ShowErrorSnackbarAsync(string message, TimeSpan? time = null)
        {
            var snackbar = Snackbar.Make(
             message,
                duration: time ?? GlobalConstants.DefaultSnackBarTime,
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Colors.Red,
                    TextColor = Colors.White
                });
            await snackbar.Show();
        }
        /// <summary>
        /// Shows an warning snackbar with the specified message.
        /// </summary>
        internal static async Task ShowWarningSnackbarAsync(string message, TimeSpan? time = null)
        {
            var snackbar = Snackbar.Make(
             message,
                duration: time ?? GlobalConstants.DefaultSnackBarTime,
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Colors.Gold,
                    TextColor = Colors.White
                });
            await snackbar.Show();
        }
        /// <summary> 
        /// Shows an info snackbar with the specified message.
        /// </summary>
        internal static async Task ShowInfoSnackbarAsync(string message, TimeSpan? time = null)
        {
            var snackbar = Snackbar.Make(
             message,
                duration: time ?? GlobalConstants.DefaultSnackBarTime,
                visualOptions: new SnackbarOptions
                {
                    BackgroundColor = Colors.AliceBlue,
                    TextColor = Colors.White
                });
            await snackbar.Show();
        }
    }
}
