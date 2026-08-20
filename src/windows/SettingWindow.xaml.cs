using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;

using Button = System.Windows.Controls.Button;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace LiveCaptionsTranslator
{
    public partial class SettingWindow : FluentWindow
    {
        private Button? currentSelected;
        private Dictionary<string, FrameworkElement> sectionReferences = new();

        public SettingWindow()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;

            Loaded += (_, _) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                Initialize();
                SelectButton(PromptButton);
            };
        }

        private void Initialize()
        {
            sectionReferences = new Dictionary<string, FrameworkElement>
            {
                { "General", ContentPanel },
                { "Prompt", PromptSection },
                { "Ollama", OllamaSection }
            };

            SwitchConfig("Ollama", Translator.Setting.ConfigIndices["Ollama"]);
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            var configs = Translator.Setting.Configs["Ollama"];
            int configIndex = Translator.Setting.ConfigIndices["Ollama"];
            configs.Insert(configIndex + 1, new OllamaConfig());
            SwitchConfig("Ollama", configIndex + 1);
            Translator.Setting.OnPropertyChanged("Configs");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var configs = Translator.Setting.Configs["Ollama"];
            int configIndex = Translator.Setting.ConfigIndices["Ollama"];
            if (configs.Count <= 1)
            {
                OllamaDeleteFlyout.Show();
                return;
            }

            configs.RemoveAt(configIndex);
            SwitchConfig("Ollama", Math.Max(0, Math.Min(configs.Count - 1, configIndex)));
            Translator.Setting.OnPropertyChanged("Configs");
        }

        private void PriorButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchConfig("Ollama", Translator.Setting.ConfigIndices["Ollama"] - 1);
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchConfig("Ollama", Translator.Setting.ConfigIndices["Ollama"] + 1);
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string targetSection)
                return;

            SelectButton(button);
            if (sectionReferences.TryGetValue(targetSection, out FrameworkElement? element))
                element.BringIntoView();
        }

        private void OllamaAPIUrlInfo_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            OllamaAPIUrlInfoFlyout.Show();
        }

        private void OllamaAPIUrlInfo_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            OllamaAPIUrlInfoFlyout.Hide();
        }

        private void SwitchConfig(string apiName, int index)
        {
            var configs = Translator.Setting.Configs[apiName];
            if (index < 0 || index >= configs.Count)
                return;

            Translator.Setting.ConfigIndices[apiName] = index;
            OllamaIndex.Text = $"{index + 1}/{configs.Count}";
            Translator.Setting.OnPropertyChanged(null);
        }

        private void SelectButton(Button button)
        {
            if (currentSelected != null)
                currentSelected.Background = new SolidColorBrush(Colors.Transparent);
            button.Background = (Brush)FindResource("ControlFillColorSecondaryBrush");
            currentSelected = button;
        }
    }
}
