namespace GermanToolbox
{
    public static class AndroidSoftInputModeService
    {
        public static void UseOverlayMode()
        {
#if ANDROID
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?
                .Window?
                .SetSoftInputMode(Android.Views.SoftInput.AdjustNothing);
#endif
        }

        public static void UseResizeMode()
        {
#if ANDROID
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?
                .Window?
                .SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
#endif
        }
    }
}
