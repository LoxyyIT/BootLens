using System.Windows;

namespace BootLens.App;

public partial class ActionConfirmationWindow : Window
{
    public string DontAskText { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public bool DontAskAgain => DontAskCheckBox.IsChecked == true;

    public ActionConfirmationWindow(string title, string subtitle, string warning, string currentLabel, string current, string nextLabel, string next, string dontAskText, string confirmText, string cancelText)
    {
        InitializeComponent();
        DontAskText = dontAskText;
        ConfirmText = confirmText;
        CancelText = cancelText;
        DataContext = this;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        WarningText.Text = warning;
        CurrentLabel.Text = currentLabel;
        CurrentText.Text = current;
        NextLabel.Text = nextLabel;
        NextText.Text = next;
        if (string.IsNullOrWhiteSpace(DontAskText)) DontAskCheckBox.Visibility = Visibility.Collapsed;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
