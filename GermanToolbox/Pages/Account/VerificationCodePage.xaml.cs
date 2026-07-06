namespace GermanToolbox
{
    public partial class VerificationCodePage : ContentPage, IQueryAttributable
    {
        private const int ResendDelaySeconds = 60;

        private Entry[] digitEntries = [];
        private Border[] digitBorders = [];
        private CancellationTokenSource? countdownCancellation;
        private int remainingSeconds = ResendDelaySeconds;
        private bool isUpdatingDigit;

        public VerificationCodePage()
        {
            InitializeComponent();

            digitEntries =
            [
                DigitOneEntry,
                DigitTwoEntry,
                DigitThreeEntry,
                DigitFourEntry,
                DigitFiveEntry,
                DigitSixEntry
            ];
            digitBorders =
            [
                DigitOneBorder,
                DigitTwoBorder,
                DigitThreeBorder,
                DigitFourBorder,
                DigitFiveBorder,
                DigitSixBorder
            ];
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("email", out var emailValue) &&
                emailValue is string email &&
                !string.IsNullOrWhiteSpace(email))
            {
                EmailLabel.Text = Uri.UnescapeDataString(email);
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            StartCountdown();
            Dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(250),
                () => DigitOneEntry.Focus());
        }

        protected override void OnDisappearing()
        {
            countdownCancellation?.Cancel();
            countdownCancellation?.Dispose();
            countdownCancellation = null;
            base.OnDisappearing();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private void OnDigitTextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUpdatingDigit || sender is not Entry entry)
            {
                return;
            }

            var index = Array.IndexOf(digitEntries, entry);
            if (index < 0)
            {
                return;
            }

            var digit = e.NewTextValue?.FirstOrDefault(char.IsDigit) ?? '\0';
            var normalizedText = char.IsDigit(digit) ? digit.ToString() : string.Empty;

            if (!string.Equals(entry.Text, normalizedText, StringComparison.Ordinal))
            {
                isUpdatingDigit = true;
                entry.Text = normalizedText;
                isUpdatingDigit = false;
            }

            if (normalizedText.Length == 1 && index < digitEntries.Length - 1)
            {
                digitEntries[index + 1].Focus();
            }
            else if (normalizedText.Length == 0 && index > 0)
            {
                digitEntries[index - 1].Focus();
            }

            SetCodeState(isError: false);
            VerifyButton.IsEnabled = digitEntries.All(
                digitEntry => digitEntry.Text?.Length == 1);
        }

        private async void OnVerifyClicked(object sender, EventArgs e)
        {
            var code = string.Concat(digitEntries.Select(entry => entry.Text));
            if (code.Length != digitEntries.Length)
            {
                SetCodeState(isError: true, "Please enter all six digits.");
                return;
            }

            SetCodeState(
                isError: true,
                "Code validation is ready for the authentication service to be connected.");

            await DisplayAlert(
                "Verification pending",
                "The six-digit code UI is complete, but a server is required to validate the code securely.",
                "OK");
        }

        private void OnResendTapped(object sender, TappedEventArgs e)
        {
            if (remainingSeconds > 0)
            {
                return;
            }

            ClearCode();
            SetCodeState(
                isError: false,
                "A new code request is ready for the authentication service.");
            remainingSeconds = ResendDelaySeconds;
            StartCountdown();
            DigitOneEntry.Focus();
        }

        private void StartCountdown()
        {
            countdownCancellation?.Cancel();
            countdownCancellation?.Dispose();
            countdownCancellation = new CancellationTokenSource();
            _ = RunCountdownAsync(countdownCancellation.Token);
        }

        private async Task RunCountdownAsync(CancellationToken cancellationToken)
        {
            UpdateCountdownUi();

            try
            {
                while (remainingSeconds > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    remainingSeconds--;
                    Dispatcher.Dispatch(UpdateCountdownUi);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void UpdateCountdownUi()
        {
            var canResend = remainingSeconds <= 0;
            CountdownLabel.IsVisible = !canResend;
            CountdownLabel.Text =
                $"Resend available in {TimeSpan.FromSeconds(remainingSeconds):mm\\:ss}";
            ResendLabel.Opacity = canResend ? 1 : 0.45;
        }

        private void ClearCode()
        {
            isUpdatingDigit = true;
            foreach (var entry in digitEntries)
            {
                entry.Text = string.Empty;
            }

            isUpdatingDigit = false;
            VerifyButton.IsEnabled = false;
        }

        private void SetCodeState(bool isError, string? message = null)
        {
            var strokeColor = isError ? Color.FromArgb("#C43D36") : Color.FromArgb("#DDE8F5");
            foreach (var border in digitBorders)
            {
                border.Stroke = strokeColor;
            }

            CodeStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
            CodeStatusLabel.Text = message ?? string.Empty;
            CodeStatusLabel.TextColor =
                isError ? Color.FromArgb("#C43D36") : Color.FromArgb("#2E7D32");
        }
    }
}
