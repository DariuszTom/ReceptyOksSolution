using ReceptyOks.Views;

namespace ReceptyOks;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		
		// Rejestracja tras dla nawigacji
		Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
		Routing.RegisterRoute(nameof(RecipeEditPage), typeof(RecipeEditPage));
		Routing.RegisterRoute(nameof(CategoryEditPage), typeof(CategoryEditPage));
		Routing.RegisterRoute(nameof(RichEditorTestPage), typeof(RichEditorTestPage));
	}
}
