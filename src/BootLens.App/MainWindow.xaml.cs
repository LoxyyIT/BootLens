using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BootLens.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public Task InitializeAsync() => _viewModel.InitializeAsync();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || sender is not ComboBox comboBox || comboBox.SelectedValue is not string language || language == viewModel.CurrentLanguage) return;
        viewModel.SetLanguageCommand.Execute(language);
    }

    private void ActionToggle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.IsEnabled && checkBox.Command?.CanExecute(checkBox.CommandParameter) == true)
        {
            checkBox.Command.Execute(checkBox.CommandParameter);
            e.Handled = true;
        }
    }

    private void ActionToggle_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || sender is not CheckBox checkBox || !checkBox.IsEnabled || checkBox.Command?.CanExecute(checkBox.CommandParameter) != true) return;
        checkBox.Command.Execute(checkBox.CommandParameter);
        e.Handled = true;
    }
}
