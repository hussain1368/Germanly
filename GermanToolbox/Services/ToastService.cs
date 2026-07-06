using Microsoft.Maui.Controls.Shapes;

namespace GermanToolbox
{
    public static class ToastService
    {
        private const string ToastStyleId = "InAppToast";

        public static async Task ShowAsync(
            ContentPage page,
            string message,
            int durationMilliseconds = 2200)
        {
            if (page.Content is not Layout root)
            {
                return;
            }

            var existingToast = root.Children
                .OfType<View>()
                .FirstOrDefault(view => view.StyleId == ToastStyleId);
            if (existingToast is not null)
            {
                root.Children.Remove(existingToast);
            }

            var toast = new Border
            {
                StyleId = ToastStyleId,
                BackgroundColor = Color.FromArgb("#171717"),
                HorizontalOptions = LayoutOptions.Center,
                InputTransparent = true,
                Margin = new Thickness(20, 0, 20, 72),
                MaximumWidthRequest = 420,
                Opacity = 0,
                Padding = new Thickness(14, 10),
                Stroke = Color.FromArgb("#00000000"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle
                {
                    CornerRadius = 8
                },
                TranslationY = 10,
                VerticalOptions = LayoutOptions.End,
                Content = new Label
                {
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 13,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.WordWrap,
                    Text = message,
                    TextColor = Colors.White
                },
                Shadow = new Shadow
                {
                    Brush = Color.FromArgb("#44000000"),
                    Offset = new Point(0, 10),
                    Opacity = 0.26f,
                    Radius = 18
                }
            };

            if (root is Grid grid)
            {
                Grid.SetRowSpan(toast, Math.Max(1, grid.RowDefinitions.Count));
                Grid.SetColumnSpan(toast, Math.Max(1, grid.ColumnDefinitions.Count));
            }

            root.Children.Add(toast);

            await Task.WhenAll(
                toast.FadeTo(1, 120, Easing.CubicOut),
                toast.TranslateTo(0, 0, 120, Easing.CubicOut));
            await Task.Delay(durationMilliseconds);

            if (root.Children.Contains(toast))
            {
                await Task.WhenAll(
                    toast.FadeTo(0, 140, Easing.CubicIn),
                    toast.TranslateTo(0, 10, 140, Easing.CubicIn));
                root.Children.Remove(toast);
            }
        }
    }
}
