using System.Windows;

namespace SoundboardApp
{
    public partial class RenameDialog : Window
    {
        public string ResultName { get; private set; } = string.Empty;

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultName = string.IsNullOrWhiteSpace(NameTextBox.Text) ? ResultName : NameTextBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
