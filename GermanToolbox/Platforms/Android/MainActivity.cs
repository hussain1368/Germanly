using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace GermanToolbox
{
    [Activity(Theme = "@style/SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustNothing, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AndroidSoftInputModeService.UseOverlayMode();
            ConfigureSystemBars();
        }

        protected override void OnResume()
        {
            base.OnResume();
            ConfigureSystemBars();
        }

        private void ConfigureSystemBars()
        {
            var window = Window;
            if (window is null)
            {
                return;
            }

            var systemBarColor = Android.Graphics.Color.ParseColor("#FAFAF8");
            window.SetStatusBarColor(systemBarColor);
            window.SetNavigationBarColor(systemBarColor);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var controller = window.InsetsController;
                if (controller is not null)
                {
                    controller.SetSystemBarsAppearance(
                        (int)WindowInsetsControllerAppearance.LightStatusBars,
                        (int)WindowInsetsControllerAppearance.LightStatusBars);
                }

                return;
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
#pragma warning disable CS0618
                var flags = (int)window.DecorView.SystemUiVisibility;
                flags |= (int)SystemUiFlags.LightStatusBar;
                window.DecorView.SystemUiVisibility = (StatusBarVisibility)flags;
#pragma warning restore CS0618
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (AndroidGoogleNativeSignInService.TryHandleActivityResult(requestCode, resultCode, data))
            {
                return;
            }

            base.OnActivityResult(requestCode, resultCode, data);
        }
    }
}
