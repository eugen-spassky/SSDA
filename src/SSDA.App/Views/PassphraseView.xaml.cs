using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SSDA.App.ViewModels;

namespace SSDA.App.Views;

public partial class PassphraseView : UserControl
{
    public PassphraseView()
    {
        InitializeComponent();
        Loaded += (_, _) => PassBox.Focus();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e) => Submit();

    private void PassBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Submit();
    }

    private void Submit()
    {
        if (DataContext is PassphraseViewModel vm && vm.UnlockCommand.CanExecute(PassBox.Password))
            vm.UnlockCommand.Execute(PassBox.Password);
    }
}
