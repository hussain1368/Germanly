namespace GermanToolbox
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell())
            {
                Width = 430,
                Height = 820,
                MinimumWidth = 360,
                MinimumHeight = 640
            };
        }
    }
}
