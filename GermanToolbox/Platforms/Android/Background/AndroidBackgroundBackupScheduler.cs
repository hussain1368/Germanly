using AndroidX.Work;
using Java.Util.Concurrent;

namespace GermanToolbox
{
    public sealed class AndroidBackgroundBackupScheduler : IBackgroundBackupScheduler
    {
        private const string UniqueWorkName = "GermanlyAutoBackup";

        public bool SupportsBackgroundScheduling => true;

        public void Schedule()
        {
            var constraints = new Constraints.Builder()
                .SetRequiredNetworkType(NetworkType.Connected!)
                .Build();

            var requestBuilder = PeriodicWorkRequest.Builder
                .From<AndroidAutoBackupWorker>(AutoBackupSchedule.BackupInterval);
            requestBuilder.SetInitialDelay(
                (long)AutoBackupSchedule.InitialDelay.TotalMilliseconds,
                TimeUnit.Milliseconds);
            requestBuilder.SetConstraints(constraints);
            var request = (PeriodicWorkRequest)requestBuilder.Build();

            WorkManager
                .GetInstance(Android.App.Application.Context)
                .EnqueueUniquePeriodicWork(
                    UniqueWorkName,
                    ExistingPeriodicWorkPolicy.Update!,
                    request);
        }

        public void Cancel() =>
            WorkManager
                .GetInstance(Android.App.Application.Context)
                .CancelUniqueWork(UniqueWorkName);
    }
}
