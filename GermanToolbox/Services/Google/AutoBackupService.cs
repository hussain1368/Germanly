namespace GermanToolbox
{
    public static class AutoBackupSchedule
    {
        // Automatic backups run hourly for testing now.
        // To change hourly automatic backups to daily later, replace this active line:
        
        public static readonly TimeSpan BackupInterval = TimeSpan.FromHours(1);
        public static readonly TimeSpan InitialDelay = BackupInterval;

        // Daily backup code for later:
        // public static readonly TimeSpan BackupInterval = TimeSpan.FromDays(1);
        // public static TimeSpan InitialDelay => GetDelayUntilNextLocalMidnight();

        // private static TimeSpan GetDelayUntilNextLocalMidnight()
        // {
        //     var now = DateTime.Now;
        //     return now.Date.AddDays(1) - now;
        // }
    }

    public sealed class AutoBackupService : IDisposable
    {
        private readonly PracticeSettingsService settingsService;
        private readonly GoogleAuthService googleAuthService;
        private readonly DriveBackupService driveBackupService;
        private readonly IBackgroundBackupScheduler backgroundBackupScheduler;
        private readonly SemaphoreSlim backupLock = new(1, 1);
        private readonly object startLock = new();
        private CancellationTokenSource? cancellationTokenSource;

        public AutoBackupService(
            PracticeSettingsService settingsService,
            GoogleAuthService googleAuthService,
            DriveBackupService driveBackupService,
            IBackgroundBackupScheduler backgroundBackupScheduler)
        {
            this.settingsService = settingsService;
            this.googleAuthService = googleAuthService;
            this.driveBackupService = driveBackupService;
            this.backgroundBackupScheduler = backgroundBackupScheduler;
            this.googleAuthService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        public void Start()
        {
            RefreshBackgroundSchedule();
            if (backgroundBackupScheduler.SupportsBackgroundScheduling)
            {
                return;
            }

            lock (startLock)
            {
                if (cancellationTokenSource is not null)
                {
                    return;
                }

                cancellationTokenSource = new CancellationTokenSource();
                _ = RunAsync(cancellationTokenSource.Token);
            }
        }

        public void RefreshBackgroundSchedule()
        {
            if (settingsService.AutoBackupEnabled && googleAuthService.IsSignedIn)
            {
                backgroundBackupScheduler.Schedule();
            }
            else
            {
                backgroundBackupScheduler.Cancel();
            }
        }

        public void Dispose()
        {
            googleAuthService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            lock (startLock)
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
            }

            backupLock.Dispose();
        }

        private void OnAuthenticationStateChanged(object? sender, EventArgs e) =>
            RefreshBackgroundSchedule();

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(AutoBackupSchedule.InitialDelay, cancellationToken);
                using var timer = new PeriodicTimer(AutoBackupSchedule.BackupInterval);
                do
                {
                    await TryRunAutomaticBackupAsync(cancellationToken);
                }
                while (await timer.WaitForNextTickAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task TryRunAutomaticBackupAsync(CancellationToken cancellationToken)
        {
            if (!settingsService.AutoBackupEnabled ||
                !googleAuthService.IsSignedIn ||
                !settingsService.BackupNeeded)
            {
                return;
            }

            if (!await backupLock.WaitAsync(0, cancellationToken))
            {
                return;
            }

            try
            {
                await driveBackupService.CreateAndUploadBackupAsync();
                await driveBackupService.DeleteOlderBackupsAsync();
            }
            catch
            {
                // Automatic backup retries on the next scheduled interval.
            }
            finally
            {
                backupLock.Release();
            }
        }
    }
}
