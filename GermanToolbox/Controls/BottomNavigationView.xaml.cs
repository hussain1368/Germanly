using Microsoft.Maui.Controls.Shapes;

namespace GermanToolbox
{
    public sealed class TabSelectedEventArgs : EventArgs
    {
        public TabSelectedEventArgs(string tab)
        {
            Tab = tab;
        }

        public string Tab { get; }
    }

    public partial class BottomNavigationView : ContentView
    {
        public static readonly BindableProperty SelectedTabProperty =
            BindableProperty.Create(
                nameof(SelectedTab),
                typeof(string),
                typeof(BottomNavigationView),
                "Home",
                propertyChanged: OnSelectedTabChanged);

        private readonly Color selectedBackgroundColor = Color.FromArgb("#EAF1FF");
        private readonly Color selectedIconColor = Color.FromArgb("#2563EB");
        private readonly Color unselectedIconColor = Color.FromArgb("#777777");

        public BottomNavigationView()
        {
            InitializeComponent();
            UpdateSelectedTab();
        }

        public event EventHandler<TabSelectedEventArgs>? TabSelected;

        public string SelectedTab
        {
            get => (string)GetValue(SelectedTabProperty);
            set => SetValue(SelectedTabProperty, value);
        }

        private static void OnSelectedTabChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ((BottomNavigationView)bindable).UpdateSelectedTab();
        }

        private void OnPracticeTapped(object sender, TappedEventArgs e)
        {
            SelectAndNotify("Practice");
        }

        private void OnHomeTapped(object sender, TappedEventArgs e)
        {
            SelectAndNotify("Home");
        }

        private void OnSearchTapped(object sender, TappedEventArgs e)
        {
            SelectAndNotify("Search");
        }

        private void OnSettingsTapped(object sender, TappedEventArgs e)
        {
            SelectAndNotify("Settings");
        }

        private void SelectAndNotify(string tab)
        {
            SelectedTab = tab;
            TabSelected?.Invoke(this, new TabSelectedEventArgs(tab));
        }

        private void UpdateSelectedTab()
        {
            ResetTab(PracticeTabBackground, PracticeIcon, PracticeLabel);
            ResetTab(HomeTabBackground, HomeIcon, HomeLabel);
            ResetTab(SearchTabBackground, SearchIcon, SearchLabel);
            ResetTab(SettingsTabBackground, SettingsIcon, SettingsLabel);

            switch (SelectedTab)
            {
                case "Practice":
                    SelectTab(PracticeTabBackground, PracticeIcon, PracticeLabel);
                    break;
                case "Search":
                    SelectTab(SearchTabBackground, SearchIcon, SearchLabel);
                    break;
                case "Settings":
                    SelectTab(SettingsTabBackground, SettingsIcon, SettingsLabel);
                    break;
                default:
                    SelectTab(HomeTabBackground, HomeIcon, HomeLabel);
                    break;
            }
        }

        private void SelectTab(Border tabBackground, Shape icon, Label label)
        {
            tabBackground.BackgroundColor = selectedBackgroundColor;
            icon.Stroke = selectedIconColor;
            label.TextColor = selectedIconColor;
        }

        private void ResetTab(Border tabBackground, Shape icon, Label label)
        {
            tabBackground.BackgroundColor = Colors.Transparent;
            icon.Stroke = unselectedIconColor;
            label.TextColor = unselectedIconColor;
        }
    }
}
