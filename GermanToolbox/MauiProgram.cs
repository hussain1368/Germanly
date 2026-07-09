using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;

namespace GermanToolbox
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            EntryHandler.Mapper.AppendToMapping(
                nameof(BorderlessEntry),
                static (handler, view) =>
                {
                    if (view is not BorderlessEntry)
                    {
                        return;
                    }

#if ANDROID
                    handler.PlatformView.BackgroundTintList =
                        Android.Content.Res.ColorStateList.ValueOf(
                            Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
                    handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
                    handler.PlatformView.BorderThickness =
                        new Microsoft.UI.Xaml.Thickness(0);
#endif
                });

            SwitchHandler.Mapper.AppendToMapping(
                "AlignSwitchTrackRight",
                static (handler, view) =>
                {
#if ANDROID
                    handler.PlatformView.SetPadding(0, 0, 0, 0);
                    handler.PlatformView.Gravity =
                        Android.Views.GravityFlags.Right |
                        Android.Views.GravityFlags.CenterVertical;
#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<AppDatabase>();
            builder.Services.AddSingleton<PracticeSettingsService>();
            builder.Services.AddSingleton<GoogleAuthService>();
#if ANDROID
            builder.Services.AddSingleton<IGoogleNativeSignInService, AndroidGoogleNativeSignInService>();
#else
            builder.Services.AddSingleton<IGoogleNativeSignInService, NoOpGoogleNativeSignInService>();
#endif
            builder.Services.AddSingleton<WordsDatabaseSeedService>();
            builder.Services.AddSingleton<WordRepository>();
            builder.Services.AddSingleton<DriveBackupService>();
            builder.Services.AddSingleton<TestSessionService>();
            builder.Services.AddSingleton<AnswerFeedbackService>();
            builder.Services.AddSingleton<PluralDistractorGenerator>();
            builder.Services.AddSingleton<IrregularVerbDistractorGenerator>();
            builder.Services.AddSingleton<RegularVerbFormGenerator>();

            var app = builder.Build();
            AppServices.Current = app.Services;

            return app;
        }
    }
}
