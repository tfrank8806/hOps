using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using hOps.Mobile.Services;

namespace hOps.Mobile.ViewModels
{
    public sealed class LoginViewModel : INotifyPropertyChanged
    {
        private readonly IAuthService _authService;

        private string _usernameOrEmail = string.Empty;
        private string _password = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public LoginViewModel(IAuthService authService)
        {
            _authService = authService;
            LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string UsernameOrEmail
        {
            get => _usernameOrEmail;
            set
            {
                if (_usernameOrEmail != value)
                {
                    _usernameOrEmail = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged();
                    ((Command)LoginCommand).ChangeCanExecute();
                }
            }
        }

        public ICommand LoginCommand { get; }

        private async Task LoginAsync()
        {
            if (IsBusy)
            {
                return;
            }

            ErrorMessage = string.Empty;
            IsBusy = true;

            try
            {
                var result = await _authService.LoginAsync(UsernameOrEmail, Password);
                if (result == null)
                {
                    ErrorMessage = "Unable to sign in.";
                    return;
                }

                await Shell.Current.GoToAsync($"//DashboardPage");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Login failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
