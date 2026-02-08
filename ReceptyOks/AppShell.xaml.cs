using ReceptyOks.Views;

namespace ReceptyOks;

public partial class AppShell : Shell
{
    private bool _navigated = false;
    public AppShell()
	{
		InitializeComponent();
		
		// Rejestracja tras dla nawigacji
       Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
       Routing.RegisterRoute(nameof(RecipeEditPage), typeof(RecipeEditPage));
       Routing.RegisterRoute(nameof(CategoryEditPage), typeof(CategoryEditPage));
       Routing.RegisterRoute(nameof(LogsPage), typeof(LogsPage));
       Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
       Routing.RegisterRoute("LoginPage", typeof(LoginPage));
       Routing.RegisterRoute(nameof(UserDetailsPage), typeof(UserDetailsPage));
	}
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_navigated)
        {
            _navigated = true;
            await GoToAsync("//LoginPage");
        }
    }
}
