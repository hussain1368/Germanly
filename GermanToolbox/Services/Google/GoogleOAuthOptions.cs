namespace GermanToolbox
{
    public static class GoogleOAuthOptions
    {
        public const string AndroidRedirectUri = "com.hussain.germanly://auth";

        private const string AndroidClientIdEnvironmentVariable = "GERMANLY_GOOGLE_ANDROID_CLIENT_ID";
        private const string WindowsClientIdEnvironmentVariable = "GERMANLY_GOOGLE_WINDOWS_CLIENT_ID";
        private const string WindowsClientSecretEnvironmentVariable = "GERMANLY_GOOGLE_WINDOWS_CLIENT_SECRET";

        public static string GetClientId(DevicePlatform platform) =>
            platform == DevicePlatform.Android
                ? GetEnvironmentValue(AndroidClientIdEnvironmentVariable)
                : platform == DevicePlatform.WinUI
                    ? GetEnvironmentValue(WindowsClientIdEnvironmentVariable)
                    : string.Empty;

        public static string GetClientSecret(DevicePlatform platform) =>
            platform == DevicePlatform.WinUI
                ? GetEnvironmentValue(WindowsClientSecretEnvironmentVariable)
                : string.Empty;

        private static string GetEnvironmentValue(string variableName) =>
            Environment.GetEnvironmentVariable(variableName) ?? string.Empty;
    }
}