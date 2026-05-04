using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SSDA.App.ViewModels;

namespace SSDA.App.Views;

public partial class LinkAuthenticatorView : UserControl
{
    public LinkAuthenticatorView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) PassBox.Focus();
            else PassBox.Clear();
        };
    }

    private void Credentials_Click(object sender, RoutedEventArgs e) => SubmitCredentials();
    private void Sms_Click(object sender, RoutedEventArgs e) => SubmitSms();

    private void PassBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SubmitCredentials();
    }

    private void SmsBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) SubmitSms();
    }

    private void SubmitCredentials()
    {
        if (DataContext is not LinkAuthenticatorViewModel vm) return;
        if (vm.CredentialsCommand.CanExecute(PassBox.Password))
            vm.CredentialsCommand.Execute(PassBox.Password);
    }

    private void SubmitSms()
    {
        if (DataContext is not LinkAuthenticatorViewModel vm) return;
        if (vm.SmsCommand.CanExecute(SmsBox.Text))
            vm.SmsCommand.Execute(SmsBox.Text);
    }
}
