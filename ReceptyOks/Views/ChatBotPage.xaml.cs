using ReceptyOks.ViewModels;
using UraniumUI.Pages;

namespace ReceptyOks.Views;

public partial class ChatBotPage :UraniumContentPage

{
	public ChatBotPage(ChatBotViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (BindingContext is ChatBotViewModel vm)
		{
			try
			{
				await vm.InitializeAsync();
			}
			catch
			{
				// Initialization errors are handled by the ViewModel; swallow here to avoid crashing the UI
			}
		}
	}
}