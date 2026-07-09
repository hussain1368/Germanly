using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Storage;

namespace GermanToolbox
{
    public sealed class GoogleAuthService
    {
        private const string CompletionPageAssetName = "google-signin-complete.html";
        private const string ProfilePhotoDirectoryName = "auth";
        private const string ProfilePhotoFileName = "google-profile-photo";
        private const string EmailPreferenceKey = "GoogleAuth.Email";
        private const string DisplayNamePreferenceKey = "GoogleAuth.DisplayName";
        private const string FirstNamePreferenceKey = "GoogleAuth.FirstName";
        private const string PhotoPathPreferenceKey = "GoogleAuth.PhotoPath";
        private const string AccessTokenStorageKey = "GoogleAuth.AccessToken";
        private const string RefreshTokenStorageKey = "GoogleAuth.RefreshToken";
        private const string IdTokenStorageKey = "GoogleAuth.IdToken";
        private static readonly Uri UserInfoEndpoint =
            new("https://openidconnect.googleapis.com/v1/userinfo");
        private static readonly Uri TokenEndpoint =
            new("https://oauth2.googleapis.com/token");
        private static readonly string[] Scopes =
        [
            "openid",
            "https://www.googleapis.com/auth/userinfo.email",
            "https://www.googleapis.com/auth/userinfo.profile",
            "https://www.googleapis.com/auth/drive.appdata"
        ];

        private readonly HttpClient httpClient = new();
        private readonly IGoogleNativeSignInService nativeSignInService;
        private readonly GoogleOAuthOptions googleOAuthOptions;
        private CancellationTokenSource? pendingSignInCancellationSource;
        private GoogleSignedInUser? currentUser;

        public GoogleAuthService(
            IGoogleNativeSignInService nativeSignInService,
            GoogleOAuthOptions googleOAuthOptions)
        {
            this.nativeSignInService = nativeSignInService;
            this.googleOAuthOptions = googleOAuthOptions;
            var email = Preferences.Default.Get(EmailPreferenceKey, string.Empty);
            var displayName = Preferences.Default.Get(DisplayNamePreferenceKey, string.Empty);
            var firstName = Preferences.Default.Get(FirstNamePreferenceKey, string.Empty);
            var photoPath = Preferences.Default.Get(PhotoPathPreferenceKey, string.Empty);

            if (!string.IsNullOrWhiteSpace(email))
            {
                currentUser = new GoogleSignedInUser(
                    email,
                    string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                    string.IsNullOrWhiteSpace(firstName)
                        ? GetFallbackFirstName(displayName, email)
                        : firstName,
                    string.IsNullOrWhiteSpace(photoPath) ? null : photoPath);
            }
        }

        public event EventHandler? AuthenticationStateChanged;

        public GoogleSignedInUser? CurrentUser => currentUser;

        public bool IsSignedIn => currentUser is not null;

        public void CancelPendingSignIn() => pendingSignInCancellationSource?.Cancel();

        public async Task<string> GetValidAccessTokenAsync(bool forceRefresh = false)
        {
            var accessToken = await SecureStorage.Default.GetAsync(AccessTokenStorageKey) ?? string.Empty;
            if (DeviceInfo.Current.Platform == DevicePlatform.Android && nativeSignInService.IsSupported)
            {
                if (!forceRefresh && !string.IsNullOrWhiteSpace(accessToken))
                {
                    return accessToken;
                }

                if (currentUser is null)
                {
                    throw new InvalidOperationException("Google Drive access failed: sign in with Google first.");
                }

                var nativeAccessToken = await nativeSignInService.GetAccessTokenAsync(
                    currentUser.Email,
                    accessToken,
                    forceRefresh);
                if (string.IsNullOrWhiteSpace(nativeAccessToken))
                {
                    throw new InvalidOperationException("Google Drive access failed: no access token was returned.");
                }

                await SecureStorage.Default.SetAsync(AccessTokenStorageKey, nativeAccessToken);
                return nativeAccessToken;
            }

            if (!forceRefresh && !string.IsNullOrWhiteSpace(accessToken))
            {
                return accessToken;
            }

            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenStorageKey) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    return accessToken;
                }

                throw new InvalidOperationException("Google Drive access failed: no access token is available.");
            }

            var refreshedToken = await RefreshAccessTokenAsync(refreshToken);
            await SecureStorage.Default.SetAsync(AccessTokenStorageKey, refreshedToken);
            return refreshedToken;
        }

        public async Task<GoogleSignedInUser> SignInAsync()
        {
            pendingSignInCancellationSource?.Dispose();
            pendingSignInCancellationSource = new CancellationTokenSource();
            var platform = DeviceInfo.Current.Platform;
            try
            {
                if (platform == DevicePlatform.Android && nativeSignInService.IsSupported)
                {
                    var androidUser = await nativeSignInService.SignInAsync();
                    var persistedAndroidUser = await PersistSessionAsync(
                        androidUser,
                        new TokenResponse(string.Empty, null, null));
                    currentUser = persistedAndroidUser;
                    RaiseAuthenticationStateChanged();
                    return persistedAndroidUser;
                }

                var clientId = googleOAuthOptions.GetClientId(platform);
                var clientSecret = googleOAuthOptions.GetClientSecret(platform);
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new InvalidOperationException(
                        $"Set the Google OAuth client ID for {platform} in GoogleOAuthOptions before signing in.");
                }

                if (platform == DevicePlatform.WinUI && string.IsNullOrWhiteSpace(clientSecret))
                {
                    throw new InvalidOperationException(
                        "Set the Windows desktop OAuth client secret in GoogleOAuthOptions before signing in on Windows.");
                }

                var codeVerifier = CreateRandomToken();
                var state = CreateRandomToken();

                AuthorizationCodeResult authorizationCode = platform == DevicePlatform.WinUI
                    ? await AuthenticateOnWindowsAsync(
                        clientId,
                        codeVerifier,
                        state,
                        pendingSignInCancellationSource.Token)
                    : await AuthenticateWithWebAuthenticatorAsync(clientId, codeVerifier, state);

                var tokenResponse = await ExchangeCodeAsync(
                    clientId,
                    clientSecret,
                    authorizationCode.RedirectUri,
                    authorizationCode.Code,
                    codeVerifier);

                var user = await LoadUserAsync(tokenResponse.AccessToken);
                var persistedUser = await PersistSessionAsync(user, tokenResponse);
                currentUser = persistedUser;
                RaiseAuthenticationStateChanged();
                return persistedUser;
            }
            finally
            {
                pendingSignInCancellationSource?.Dispose();
                pendingSignInCancellationSource = null;
            }
        }

        public async Task SignOutAsync()
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.Android && nativeSignInService.IsSupported)
            {
                await nativeSignInService.SignOutAsync();
            }

            currentUser = null;
            Preferences.Default.Remove(EmailPreferenceKey);
            Preferences.Default.Remove(DisplayNamePreferenceKey);
            Preferences.Default.Remove(FirstNamePreferenceKey);
            Preferences.Default.Remove(PhotoPathPreferenceKey);
            SecureStorage.Default.Remove(AccessTokenStorageKey);
            SecureStorage.Default.Remove(RefreshTokenStorageKey);
            SecureStorage.Default.Remove(IdTokenStorageKey);
            DeleteCachedProfilePhoto();
            await Task.CompletedTask;
            RaiseAuthenticationStateChanged();
        }

        private async Task<AuthorizationCodeResult> AuthenticateWithWebAuthenticatorAsync(
            string clientId,
            string codeVerifier,
            string expectedState)
        {
            var redirectUri = GoogleOAuthOptions.AndroidRedirectUri;
            var callbackUri = new Uri(redirectUri);
            var authUri = BuildAuthorizationUri(clientId, redirectUri, codeVerifier, expectedState);
            var result = await WebAuthenticator.Default.AuthenticateAsync(authUri, callbackUri);

            if (!result.Properties.TryGetValue("state", out var returnedState) ||
                !string.Equals(returnedState, expectedState, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Google sign-in returned an invalid state value.");
            }

            if (!result.Properties.TryGetValue("code", out var code) ||
                string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Google sign-in did not return an authorization code.");
            }

            return new AuthorizationCodeResult(code, redirectUri);
        }

        private async Task<AuthorizationCodeResult> AuthenticateOnWindowsAsync(
            string clientId,
            string codeVerifier,
            string expectedState,
            CancellationToken cancellationToken)
        {
            var loopbackUri = $"http://127.0.0.1:{GetFreeTcpPort()}/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(loopbackUri);
            listener.Start();

            var authUri = BuildAuthorizationUri(clientId, loopbackUri, codeVerifier, expectedState);
            await Browser.Default.OpenAsync(authUri, BrowserLaunchMode.SystemPreferred);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token,
                cancellationToken);
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Google sign-in was cancelled.", ex, cancellationToken);
                }

                throw new TimeoutException("Google sign-in timed out before the browser returned to the app.", ex);
            }

            try
            {
                var returnedState = context.Request.QueryString["state"];
                var code = context.Request.QueryString["code"];
                var error = context.Request.QueryString["error"];
                var errorDescription = context.Request.QueryString["error_description"];
                await WriteBrowserResponseAsync(context.Response);

                if (!string.IsNullOrWhiteSpace(error))
                {
                    if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new OperationCanceledException("Google sign-in was cancelled.");
                    }

                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(errorDescription)
                            ? $"Google sign-in failed: {error}"
                            : $"Google sign-in failed: {errorDescription}");
                }

                if (!string.Equals(returnedState, expectedState, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Google sign-in returned an invalid state value.");
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    throw new InvalidOperationException("Google sign-in did not return an authorization code.");
                }

                return new AuthorizationCodeResult(code, loopbackUri);
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task WriteBrowserResponseAsync(HttpListenerResponse response)
        {
            var html = await LoadCompletionPageHtmlAsync();
            var buffer = Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }

        private static async Task<string> LoadCompletionPageHtmlAsync()
        {
            try
            {
                await using var stream = await FileSystem.OpenAppPackageFileAsync(CompletionPageAssetName);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch
            {
                return "<html><body><h2>Germanly sign-in complete</h2><p>You can return to the app now.</p></body></html>";
            }
        }

        private async Task<TokenResponse> ExchangeCodeAsync(
            string clientId,
            string clientSecret,
            string redirectUri,
            string code,
            string codeVerifier)
        {
            var formData = new List<KeyValuePair<string, string>>
            {
                new("client_id", clientId),
                new("code", code),
                new("code_verifier", codeVerifier),
                new("grant_type", "authorization_code"),
                new("redirect_uri", redirectUri)
            };

            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                formData.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            using var response = await httpClient.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Google token exchange failed: {payload}");
            }

            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            return new TokenResponse(
                root.GetProperty("access_token").GetString() ?? string.Empty,
                root.TryGetProperty("refresh_token", out var refreshToken)
                    ? refreshToken.GetString()
                    : null,
                root.TryGetProperty("id_token", out var idToken)
                    ? idToken.GetString()
                    : null);
        }

        private async Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            var platform = DeviceInfo.Current.Platform;
            var clientId = googleOAuthOptions.GetClientId(platform);
            var clientSecret = googleOAuthOptions.GetClientSecret(platform);

            var formData = new List<KeyValuePair<string, string>>
            {
                new("client_id", clientId),
                new("refresh_token", refreshToken),
                new("grant_type", "refresh_token")
            };

            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                formData.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(formData)
            };

            using var response = await httpClient.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Google token refresh failed: {payload}");
            }

            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            var accessToken = root.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException("Google token refresh failed: no access token was returned.");
            }

            if (root.TryGetProperty("refresh_token", out var newRefreshToken) &&
                !string.IsNullOrWhiteSpace(newRefreshToken.GetString()))
            {
                await SecureStorage.Default.SetAsync(RefreshTokenStorageKey, newRefreshToken.GetString()!);
            }

            return accessToken;
        }

        private async Task<GoogleSignedInUser> LoadUserAsync(string accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Google user info lookup failed: {payload}");
            }

            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            var email = root.TryGetProperty("email", out var emailProperty)
                ? emailProperty.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("Google sign-in succeeded, but no email address was returned.");
            }

            var displayName = root.TryGetProperty("name", out var nameProperty)
                ? nameProperty.GetString()
                : null;
            var firstName = root.TryGetProperty("given_name", out var givenNameProperty)
                ? givenNameProperty.GetString()
                : null;
            var photoUrl = root.TryGetProperty("picture", out var pictureProperty)
                ? pictureProperty.GetString()
                : null;

            return new GoogleSignedInUser(
                email,
                string.IsNullOrWhiteSpace(displayName) ? email : displayName,
                GetFallbackFirstName(firstName ?? displayName, email),
                string.IsNullOrWhiteSpace(photoUrl) ? null : photoUrl);
        }

        private async Task<GoogleSignedInUser> PersistSessionAsync(
            GoogleSignedInUser user,
            TokenResponse tokenResponse)
        {
            var cachedPhotoPath = await CacheProfilePhotoAsync(user.PhotoPath);
            var persistedUser = user with { PhotoPath = cachedPhotoPath };

            Preferences.Default.Set(EmailPreferenceKey, persistedUser.Email);
            Preferences.Default.Set(DisplayNamePreferenceKey, persistedUser.DisplayName);
            Preferences.Default.Set(FirstNamePreferenceKey, persistedUser.FirstName);
            Preferences.Default.Set(PhotoPathPreferenceKey, persistedUser.PhotoPath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                SecureStorage.Default.Remove(AccessTokenStorageKey);
            }
            else
            {
                await SecureStorage.Default.SetAsync(AccessTokenStorageKey, tokenResponse.AccessToken);
            }

            if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                await SecureStorage.Default.SetAsync(
                    RefreshTokenStorageKey,
                    tokenResponse.RefreshToken);
            }

            if (!string.IsNullOrWhiteSpace(tokenResponse.IdToken))
            {
                await SecureStorage.Default.SetAsync(IdTokenStorageKey, tokenResponse.IdToken);
            }

            return persistedUser;
        }

        private static Uri BuildAuthorizationUri(
            string clientId,
            string redirectUri,
            string codeVerifier,
            string state)
        {
            var codeChallenge = CreateCodeChallenge(codeVerifier);
            var scopeValue = string.Join(" ", Scopes);
            var query = string.Join(
                "&",
                new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["redirect_uri"] = redirectUri,
                    ["response_type"] = "code",
                    ["scope"] = scopeValue,
                    ["state"] = state,
                    ["code_challenge"] = codeChallenge,
                    ["code_challenge_method"] = "S256",
                    ["access_type"] = "offline",
                    ["include_granted_scopes"] = "true",
                    ["prompt"] = "consent"
                }.Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

            return new Uri($"https://accounts.google.com/o/oauth2/v2/auth?{query}");
        }

        private void RaiseAuthenticationStateChanged()
        {
            if (MainThread.IsMainThread)
            {
                AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
                AuthenticationStateChanged?.Invoke(this, EventArgs.Empty));
        }

        private static string CreateRandomToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Base64UrlEncode(bytes);
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

        private static string GetFallbackFirstName(string? displayName, string email)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var firstPart = displayName
                    .Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstPart))
                {
                    return firstPart;
                }
            }

            var atIndex = email.IndexOf('@');
            return atIndex > 0 ? email[..atIndex] : email;
        }

        private async Task<string?> CacheProfilePhotoAsync(string? remotePhotoUrl)
        {
            DeleteCachedProfilePhoto();

            if (string.IsNullOrWhiteSpace(remotePhotoUrl))
            {
                return null;
            }

            try
            {
                var photoDirectory = Path.Combine(FileSystem.AppDataDirectory, ProfilePhotoDirectoryName);
                Directory.CreateDirectory(photoDirectory);

                var extension = Path.GetExtension(new Uri(remotePhotoUrl).AbsolutePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".jpg";
                }

                var cacheKey = Guid.NewGuid().ToString("N")[..8];
                var photoPath = Path.Combine(photoDirectory, $"{ProfilePhotoFileName}-{cacheKey}{extension}");
                var bytes = await httpClient.GetByteArrayAsync(remotePhotoUrl);
                await File.WriteAllBytesAsync(photoPath, bytes);
                return photoPath;
            }
            catch
            {
                return null;
            }
        }

        private static void DeleteCachedProfilePhoto()
        {
            var photoDirectory = Path.Combine(FileSystem.AppDataDirectory, ProfilePhotoDirectoryName);
            if (!Directory.Exists(photoDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(photoDirectory, $"{ProfilePhotoFileName}*.*"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static int GetFreeTcpPort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private sealed record AuthorizationCodeResult(string Code, string RedirectUri);

        private sealed record TokenResponse(
            string AccessToken,
            string? RefreshToken,
            string? IdToken);
    }
}