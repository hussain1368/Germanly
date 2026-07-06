using System.Globalization;

namespace GermanToolbox
{
    public partial class UserProfilePage : ContentPage
    {
        private static readonly DateOnly SampleBirthDate = new(1997, 5, 16);

        public UserProfilePage()
        {
            InitializeComponent();
            BirthDateLabel.Text = SampleBirthDate.ToString(
                "dd.MM.yyyy",
                CultureInfo.InvariantCulture);
            AgeLabel.Text = $"{CalculateAge(SampleBirthDate, DateOnly.FromDateTime(DateTime.Today))} Jahre";
        }

        private async void OnBackTapped(object sender, TappedEventArgs e) =>
            await Shell.Current.GoToAsync("..");

        private static int CalculateAge(DateOnly birthDate, DateOnly today)
        {
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }
    }
}
