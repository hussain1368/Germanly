namespace GermanToolbox
{
    public partial class VocabularyResultsPage : ContentPage
    {
        private readonly TestSessionService testSessionService;
        private readonly AnswerFeedbackService answerFeedbackService;

        public VocabularyResultsPage()
        {
            InitializeComponent();
            testSessionService = AppServices.GetRequiredService<TestSessionService>();
            answerFeedbackService = AppServices.GetRequiredService<AnswerFeedbackService>();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await answerFeedbackService.PlayResultAsync();
            if (ApplyResult())
            {
                await Task.Delay(120);
                await PerfectScoreFireworks.PlayAsync();
            }
        }

        protected override bool OnBackButtonPressed() => true;

        private async void OnBackToStartClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("../..");
        }

        private async void OnReviewWordsClicked(object sender, EventArgs e)
        {
            var session = testSessionService.StartMistakeReviewFromLastResult(PracticeMode.Meaning);
            if (session is null || session.TotalCount == 0)
            {
                await DisplayAlert("Keine Fehler", "There are no mistakes from this session to review.", "OK");
                return;
            }

            await Shell.Current.GoToAsync("..");
        }

        private bool ApplyResult()
        {
            var result = testSessionService.GetLastResult(PracticeMode.Meaning);
            if (result is null)
            {
                SetReviewWordsEnabled(false);
                return false;
            }

            AccuracyLabel.Text = $"{result.AccuracyPercent}%";
            SummaryLabel.Text = GetSummary(result);
            AccuracyProgressBar.Progress = result.Accuracy;
            CorrectCountLabel.Text = result.CorrectCount.ToString();
            MistakeCountLabel.Text = result.MistakeCount.ToString();
            TimeLabel.Text = FormatDuration(result.Duration);
            TotalCountLabel.Text = result.TotalCount.ToString();
            SetReviewWordsEnabled(result.MistakeCount > 0);
            return result.TotalCount > 0 && result.MistakeCount == 0;
        }

        private static string GetSummary(TestSessionResult result)
        {
            if (result.MistakeCount == 0)
            {
                return "Alles geschafft";
            }

            return result.AccuracyPercent >= 70 ? "Gut gemacht" : "Weiterüben";
        }

        private static string FormatDuration(TimeSpan duration) =>
            $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";

        private void SetReviewWordsEnabled(bool isEnabled)
        {
            ReviewWordsButton.IsEnabled = isEnabled;
            ReviewWordsButton.IsVisible = isEnabled;
        }
    }
}
