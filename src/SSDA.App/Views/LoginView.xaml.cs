using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SSDA.App.ViewModels;

namespace SSDA.App.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) PassBox.Focus();
            else PassBox.Clear();
        };
    }

    private void Submit_Click(object sender, RoutedEventArgs e) => Submit();

    private void PassBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Submit();
    }

    private void Submit()
    {
        if (DataContext is LoginViewModel vm && vm.SubmitCommand.CanExecute(PassBox.Password))
            vm.SubmitCommand.Execute(PassBox.Password);
    }
}
