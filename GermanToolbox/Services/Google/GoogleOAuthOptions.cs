using System.Reflection;
using System.Text.Json;

namespace GermanToolbox
{
    public sealed class GoogleOAuthOptions
    {
        private const string SettingsResourceName = "GermanToolbox.Config.google-oauth-settings.json";
        private const string WindowsSecretResourceName = "GermanToolbox.Config.google-oauth-windows-secret.txt";
        public const string AndroidRedirectUri = "com.hussain.germanly://auth";

        public string AndroidClientId { get; init; } = string.Empty;

        public string WindowsClientId { get; init; } = string.Empty;

        public string WindowsClientSecret { get; init; } = string.Empty;

        public static GoogleOAuthOptions Load()
        {
            var fileSettings = ReadSettingsFile();
            return new GoogleOAuthOptions
            {
                AndroidClientId = fileSettings.AndroidClientId.Trim(),
                WindowsClientId = fileSettings.WindowsClientId.Trim(),
                WindowsClientSecret = ReadWindowsClientSecret().Trim()
            };
        }

        public string GetClientId(DevicePlatform platform) =>
            platform == DevicePlatform.Android
                ? AndroidClientId
                : platform == DevicePlatform.WinUI
                    ? WindowsClientId
                    : string.Empty;

        public string GetClientSecret(DevicePlatform platform) =>
            platform == DevicePlatform.WinUI
                ? WindowsClientSecret
                : string.Empty;

        private static SettingsFile ReadSettingsFile()
        {
            using var stream = OpenResource(SettingsResourceName);
            if (stream is null)
            {
                return new SettingsFile();
            }

            return JsonSerializer.Deserialize<SettingsFile>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                new SettingsFile();
        }

        private static string ReadWindowsClientSecret()
        {
            using var stream = OpenResource(WindowsSecretResourceName);
            if (stream is null)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private static Stream? OpenResource(string resourceName) =>
            Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

        private sealed class SettingsFile
        {
            public string AndroidClientId { get; set; } = string.Empty;

            public string WindowsClientId { get; set; } = string.Empty;
        }
    }
}