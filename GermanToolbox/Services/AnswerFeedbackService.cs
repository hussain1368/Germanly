using System.Diagnostics;

namespace GermanToolbox
{
    public sealed class AnswerFeedbackService
    {
        private const string CorrectSound = "ding.mp3";
        private const string IncorrectSound = "biz.mp3";
        private const string ResultSound = "result.mp3";

        private readonly PracticeSettingsService settingsService;
        private readonly object cacheLock = new();
        private readonly Dictionary<string, Task<string>> cachedSoundPaths = [];

#if ANDROID
        private readonly Dictionary<string, Android.Media.MediaPlayer> players = [];
#elif IOS || MACCATALYST
        private readonly Dictionary<string, AVFoundation.AVAudioPlayer> players = [];
#elif WINDOWS
        private readonly Dictionary<string, Windows.Media.Playback.MediaPlayer> players = [];
#endif

        public AnswerFeedbackService(PracticeSettingsService settingsService)
        {
            this.settingsService = settingsService;
        }

        public Task PlayAnswerAsync(bool isCorrect)
        {
            if (!isCorrect && settingsService.VibrationsEnabled)
            {
                PerformIncorrectHaptic();
            }

            return PlaySoundAsync(isCorrect ? CorrectSound : IncorrectSound);
        }

        public Task PlayResultAsync() => PlaySoundAsync(ResultSound);

        private async Task PlaySoundAsync(string fileName)
        {
            if (!ShouldPlaySound())
            {
                return;
            }

            try
            {
                var path = await GetCachedSoundPathAsync(fileName);
                await MainThread.InvokeOnMainThreadAsync(
                    () => ShouldPlaySound()
                        ? PlayPlatformSoundAsync(path)
                        : Task.CompletedTask);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Could not play feedback sound '{fileName}': {exception}");
            }
        }

        private bool ShouldPlaySound()
        {
            if (!settingsService.SoundsEnabled)
            {
                return false;
            }

#if ANDROID
            try
            {
                var audioManager =
                    Android.App.Application.Context.GetSystemService(
                        Android.Content.Context.AudioService)
                    as Android.Media.AudioManager;

                return audioManager?.RingerMode == Android.Media.RingerMode.Normal;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Could not read Android ringer mode: {exception}");
                return false;
            }
#else
            return true;
#endif
        }

        private Task<string> GetCachedSoundPathAsync(string fileName)
        {
            lock (cacheLock)
            {
                if (!cachedSoundPaths.TryGetValue(fileName, out var pathTask))
                {
                    pathTask = CacheSoundAsync(fileName);
                    cachedSoundPaths[fileName] = pathTask;
                }

                return pathTask;
            }
        }

        private static async Task<string> CacheSoundAsync(string fileName)
        {
            var soundDirectory = Path.Combine(FileSystem.CacheDirectory, "feedback-sounds");
            Directory.CreateDirectory(soundDirectory);

            var destinationPath = Path.Combine(soundDirectory, fileName);
            await using var source =
                await FileSystem.OpenAppPackageFileAsync($"sounds/{fileName}");
            await using var destination = File.Create(destinationPath);
            await source.CopyToAsync(destination);

            return destinationPath;
        }

        private static void PerformIncorrectHaptic()
        {
            try
            {
#if ANDROID
                if (Vibration.Default.IsSupported)
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(80));
                }
#else
                if (HapticFeedback.Default.IsSupported)
                {
                    HapticFeedback.Default.Perform(HapticFeedbackType.Click);
                }
#endif
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"Could not perform incorrect-answer haptic: {exception}");
            }
        }

        private Task PlayPlatformSoundAsync(string path)
        {
#if ANDROID
            ReplacePlayer(path);
            var player = new Android.Media.MediaPlayer();
            player.SetDataSource(path);
            player.Prepare();
            players[path] = player;
            player.Start();
#elif IOS || MACCATALYST
            ReplacePlayer(path);
            var player = AVFoundation.AVAudioPlayer.FromUrl(
                Foundation.NSUrl.FromFilename(path));
            if (player is not null)
            {
                players[path] = player;
                player.PrepareToPlay();
                player.Play();
            }
#elif WINDOWS
            ReplacePlayer(path);
            return PlayWindowsSoundAsync(path);
#endif
#if !WINDOWS
            return Task.CompletedTask;
#endif
        }

#if ANDROID
        private void ReplacePlayer(string path)
        {
            if (!players.Remove(path, out var player))
            {
                return;
            }

            player.Stop();
            player.Release();
            player.Dispose();
        }
#elif IOS || MACCATALYST
        private void ReplacePlayer(string path)
        {
            if (!players.Remove(path, out var player))
            {
                return;
            }

            player.Stop();
            player.Dispose();
        }
#elif WINDOWS
        private async Task PlayWindowsSoundAsync(string path)
        {
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var player = new Windows.Media.Playback.MediaPlayer
            {
                Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(file)
            };
            players[path] = player;
            player.Play();
        }

        private void ReplacePlayer(string path)
        {
            if (!players.Remove(path, out var player))
            {
                return;
            }

            player.Pause();
            player.Dispose();
        }
#endif
    }
}
