namespace GermanToolbox
{
    public partial class SignInPage : ContentPage
    {
        private readonly List<string> birthdayMonths =
            Enumerable.Range(1, 12).Select(month => month.ToString("00")).ToList();
        private readonly List<string> birthdayYears =
            Enumerable.Range(1900, DateTime.Today.Year - 1899)
                .Reverse()
                .Select(year => year.ToString())
                .ToList();
        private DateTime selectedBirthday = new(2000, 1, 1);
        private DateTime pendingBirthday = new(2000, 1, 1);
        private bool isUpdatingBirthdayWheels;

        public SignInPage()
        {
            InitializeComponent();
            InitializeBirthdayWheels();
        }

        private async void OnBackTapped(object sender, TappedEventArgs e)
        {
            if (BirthdayWheelOverlay.IsVisible)
            {
                BirthdayWheelOverlay.IsVisible = false;
                return;
            }

            await Shell.Current.GoToAsync("..");
        }

        protected override bool OnBackButtonPressed()
        {
            if (!BirthdayWheelOverlay.IsVisible)
            {
                return base.OnBackButtonPressed();
            }

            BirthdayWheelOverlay.IsVisible = false;
            return true;
        }

        private async void OnCreateAccountClicked(object sender, EventArgs e)
        {
            var email = SignUpEmailEntry.Text?.Trim() ?? string.Empty;
            var route = string.IsNullOrWhiteSpace(email)
                ? nameof(VerificationCodePage)
                : $"{nameof(VerificationCodePage)}?email={Uri.EscapeDataString(email)}";

            await Shell.Current.GoToAsync(route);
        }

        private void InitializeBirthdayWheels()
        {
            BirthdayMonthWheel.ItemsSource = birthdayMonths;
            BirthdayYearWheel.ItemsSource = birthdayYears;
            SetBirthdayWheelPositions(selectedBirthday);
        }

        private void OnBirthdayTapped(object sender, TappedEventArgs e)
        {
            pendingBirthday = selectedBirthday;
            SetBirthdayWheelPositions(pendingBirthday);
            BirthdayWheelOverlay.IsVisible = true;
        }

        private void OnBirthdayWheelPositionChanged(object sender, PositionChangedEventArgs e)
        {
            if (isUpdatingBirthdayWheels)
            {
                return;
            }

            var month = BirthdayMonthWheel.Position + 1;
            var year = DateTime.Today.Year - BirthdayYearWheel.Position;
            var preferredDay = BirthdayDayWheel.Position + 1;
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var day = Math.Clamp(preferredDay, 1, daysInMonth);

            if (BirthdayDayWheel.ItemsSource is not IReadOnlyCollection<string> days ||
                days.Count != daysInMonth)
            {
                isUpdatingBirthdayWheels = true;
                BirthdayDayWheel.ItemsSource = Enumerable.Range(1, daysInMonth)
                    .Select(value => value.ToString("00"))
                    .ToList();
                BirthdayDayWheel.Position = day - 1;
                isUpdatingBirthdayWheels = false;
            }

            pendingBirthday = new DateTime(year, month, day);
        }

        private void SetBirthdayWheelPositions(DateTime date)
        {
            isUpdatingBirthdayWheels = true;
            BirthdayDayWheel.ItemsSource = Enumerable.Range(
                    1,
                    DateTime.DaysInMonth(date.Year, date.Month))
                .Select(day => day.ToString("00"))
                .ToList();
            BirthdayDayWheel.Position = date.Day - 1;
            BirthdayMonthWheel.Position = date.Month - 1;
            BirthdayYearWheel.Position = DateTime.Today.Year - date.Year;
            isUpdatingBirthdayWheels = false;
        }

        private void OnCancelBirthdayClicked(object sender, EventArgs e)
        {
            BirthdayWheelOverlay.IsVisible = false;
        }

        private void OnConfirmBirthdayClicked(object sender, EventArgs e)
        {
            selectedBirthday = pendingBirthday;
            BirthdayValueLabel.Text = selectedBirthday.ToString("dd.MM.yyyy");
            BirthdayWheelOverlay.IsVisible = false;
        }

        private async void OnShowSignInTapped(object sender, TappedEventArgs e)
        {
            SignUpView.IsVisible = false;
            SignInView.IsVisible = true;
            AccountSubtitle.Text = "Sign in to your account";
            await AccountScrollView.ScrollToAsync(0, 0, true);
        }

        private async void OnShowSignUpTapped(object sender, TappedEventArgs e)
        {
            SignInView.IsVisible = false;
            SignUpView.IsVisible = true;
            AccountSubtitle.Text = "Create an account";
            await AccountScrollView.ScrollToAsync(0, 0, true);
        }
    }
}
