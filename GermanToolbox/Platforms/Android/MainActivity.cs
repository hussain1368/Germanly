using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace GermanToolbox
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustNothing, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            AndroidSoftInputModeService.UseOverlayMode();
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
