using ReceptyOks.ViewModels;

namespace ReceptyOks.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viemModel)
    {
        InitializeComponent();
        BindingContext = viemModel;
    }
}