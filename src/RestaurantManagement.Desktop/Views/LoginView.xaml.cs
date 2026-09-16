using System.Windows;
using System.Windows.Controls;
using RestaurantManagement.Desktop.ViewModels;

namespace RestaurantManagement.Desktop.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = TxtPassword.Password;
        }
    }

    private void TxtConfirmPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.ConfirmPassword = TxtConfirmPassword.Password;
        }
    }

    private void DemoButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            TxtPassword.Password = vm.Password;
        }
    }
}
