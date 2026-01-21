using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class ChatBotPage : ContentPage
{
	public ChatBotPage(ChatBotViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}