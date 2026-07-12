using Android.Content;
using AndroidX.Work;

namespace GermanToolbox
{
    public sealed class AndroidAutoBackupWorker : Worker
    {
        public AndroidAutoBackupWorker(Context context, WorkerParameters workerParameters)
            : base(context, workerParameters)
        {
        }

        public override Result DoWork()
        {
            try
            {
                return RunBackupAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return Result.InvokeRetry()!;
            }
        }

        private static async Task<Result> RunBackupAsync()
        {
            var settingsService = AppServices.GetRequiredService<PracticeSettingsService>();
            var googleAuthService = AppServices.GetRequiredService<GoogleAuthService>();

            if (!settingsService.AutoBackupEnabled ||
                !googleAuthService.IsSignedIn ||
                Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return Result.InvokeSuccess()!;
            }

            // Backup-needed flag is intentionally ignored for automatic backups while testing.
            // To make automatic backups skip identical data later, uncomment this block:
            //
            // if (!settingsService.BackupNeeded)
            // {
            //     return Result.InvokeSuccess();
            // }

            var driveBackupService = AppServices.GetRequiredService<DriveBackupService>();
            await driveBackupService.CreateAndUploadBackupAsync();
            await driveBackupService.DeleteOlderBackupsAsync();

            return Result.InvokeSuccess()!;
        }
    }
}
