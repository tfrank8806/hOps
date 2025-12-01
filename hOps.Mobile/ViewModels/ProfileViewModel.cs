using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using hOps.Mobile.Services;

namespace hOps.Mobile.ViewModels;

public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private readonly ICurrentUserStore _currentUserStore;
    private readonly IAuthService _authService;

    private UserSummaryDto _user = new();
    private bool _isBusy;

    public ProfileViewModel(ICurrentUserStore currentUserStore, IAuthService authService)
    {
        _currentUserStore = currentUserStore;
        _authService = authService;
        LogoutCommand = new Command(async () => await LogoutAsync(), () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UserSummaryDto User
    {
        get => _user;
        private set
        {
            if (!ReferenceEquals(_user, value))
            {
                _user = value ?? new UserSummaryDto();
                OnPropertyChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
                ((Command)LogoutCommand).ChangeCanExecute();
            }
        }
    }

    public ICommand LogoutCommand { get; }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var current = await _currentUserStore.GetUserAsync();
            User = current ?? new UserSummaryDto();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LogoutAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//LoginPage");
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
