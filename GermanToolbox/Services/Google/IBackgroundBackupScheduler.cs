namespace GermanToolbox
{
    public interface IBackgroundBackupScheduler
    {
        bool SupportsBackgroundScheduling { get; }

        void Schedule();

        void Cancel();
    }

    public sealed class NoOpBackgroundBackupScheduler : IBackgroundBackupScheduler
    {
        public bool SupportsBackgroundScheduling => false;

        public void Schedule()
        {
        }

        public void Cancel()
        {
        }
    }
}
