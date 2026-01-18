using CommunityToolkit.Mvvm.ComponentModel;
using ReceptyOks.Services;


namespace ReceptyOks.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly BackendAuthService _backendAuth;
        public LoginViewModel(BackendAuthService backendAuth)
        {
            _backendAuth = backendAuth;
        }
    }
}
