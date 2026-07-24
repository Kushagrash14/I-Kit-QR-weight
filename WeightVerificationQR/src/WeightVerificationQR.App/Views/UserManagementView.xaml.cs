using System.Windows;
using System.Windows.Controls;
using WeightVerificationQR.App.ViewModels;

namespace WeightVerificationQR.App.Views;

public partial class UserManagementView : UserControl
{
    public UserManagementView()
    {
        InitializeComponent();
    }

    private void NewPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserManagementViewModel vm)
            vm.NewPassword = NewPasswordBox.Password;
    }
}
