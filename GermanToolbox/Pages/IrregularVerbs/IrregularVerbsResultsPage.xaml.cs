namespace GermanToolbox
{
    public partial class IrregularVerbsResultsPage : ContentPage
    {
        private readonly TestSessionService testSessionService;
        private readonly AnswerFeedbackService answerFeedbackService;

        public IrregularVerbsResultsPage()
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

        private async void OnBackToStartClicked(object sender, EventArgs e) =>
            await Shell.Current.GoToAsync("../..");

        private async void OnReviewMistakesClicked(object sender, EventArgs e)
        {
            var session = testSessionService.StartMistakeReviewFromLastResult(
                PracticeMode.IrregularVerb);
            if (session is null || session.TotalCount == 0)
            {
                await DisplayAlert(
                    "No mistakes",
                    "There are no mistakes from this session to review.",
                    "OK");
                return;
            }

            await Shell.Current.GoToAsync("..");
        }

        private bool ApplyResult()
        {
            var result = testSessionService.GetLastResult(PracticeMode.IrregularVerb);
            if (result is null)
            {
                SetReviewMistakesEnabled(false);
                return false;
            }

            AccuracyLabel.Text = $"{result.AccuracyPercent}%";
            SummaryLabel.Text = result.MistakeCount switch
            {
                0 => "Fehlerfrei",
                _ when result.AccuracyPercent >= 70 => "Gut gemacht",
                _ => "Weiterüben"
            };
            AccuracyProgressBar.Progress = result.Accuracy;
            CorrectCountLabel.Text = result.CorrectCount.ToString();
            MistakeCountLabel.Text = result.MistakeCount.ToString();
            TimeLabel.Text = $"{(int)result.Duration.TotalMinutes}:{result.Duration.Seconds:00}";
            TotalCountLabel.Text = result.TotalCount.ToString();
            SetReviewMistakesEnabled(result.MistakeCount > 0);
            return result.TotalCount > 0 && result.MistakeCount == 0;
        }

        private void SetReviewMistakesEnabled(bool enabled)
        {
            ReviewMistakesButton.IsEnabled = enabled;
            ReviewMistakesButton.IsVisible = enabled;
        }

        private static string GetFormTitle(IrregularVerbForm form) =>
            form switch
            {
                IrregularVerbForm.Prateritum => "Präteritum",
                _ => "Perfekt"
            };

        private static string GetMethodTitle(IrregularTestMethod method) =>
            method switch
            {
                IrregularTestMethod.Typing => "Eingabe",
                IrregularTestMethod.SelfAssessment => "Selbstkontrolle",
                _ => "Auswahl"
            };
    }
}
