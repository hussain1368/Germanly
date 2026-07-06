using Microsoft.Maui.Layouts;

namespace GermanToolbox
{
    public sealed class PerfectScoreFireworksView : AbsoluteLayout
    {
        private static readonly Color[] ParticleColors =
        [
            Color.FromArgb("#F2C94C"),
            Color.FromArgb("#2563EB"),
            Color.FromArgb("#D43D35"),
            Color.FromArgb("#2E7D32"),
            Color.FromArgb("#A05A9D"),
            Color.FromArgb("#FFFFFF")
        ];

        private bool isPlaying;

        public PerfectScoreFireworksView()
        {
            InputTransparent = true;
            IsVisible = false;
        }

        public async Task PlayAsync()
        {
            if (isPlaying)
            {
                return;
            }

            isPlaying = true;
            Children.Clear();
            IsVisible = true;

            try
            {
                var animations = new List<Task>();
                AddBurst(animations, 0.22, 0.24, delay: 0, radius: 82);
                AddBurst(animations, 0.78, 0.2, delay: 150, radius: 74);
                AddBurst(animations, 0.5, 0.42, delay: 300, radius: 96);
                AddBurst(animations, 0.18, 0.58, delay: 620, radius: 70);
                AddBurst(animations, 0.82, 0.56, delay: 800, radius: 78);
                AddBurst(animations, 0.5, 0.28, delay: 1040, radius: 88);
                await Task.WhenAll(animations);
                await Task.Delay(160);
            }
            finally
            {
                IsVisible = false;
                Children.Clear();
                isPlaying = false;
            }
        }

        private void AddBurst(
            ICollection<Task> animations,
            double originX,
            double originY,
            int delay,
            double radius)
        {
            const int particleCount = 12;

            for (var index = 0; index < particleCount; index++)
            {
                var angle = (Math.PI * 2 * index / particleCount) + (index % 2 * 0.08);
                var particleRadius = radius * (index % 3 == 0 ? 0.72 : 1);
                var particle = new BoxView
                {
                    BackgroundColor = ParticleColors[index % ParticleColors.Length],
                    CornerRadius = 4,
                    HeightRequest = 7,
                    Opacity = 0,
                    Scale = 0.35,
                    WidthRequest = 7
                };

                AbsoluteLayout.SetLayoutBounds(
                    (BindableObject)particle,
                    new Rect(originX, originY, 7, 7));
                AbsoluteLayout.SetLayoutFlags(
                    (BindableObject)particle,
                    AbsoluteLayoutFlags.PositionProportional);
                Children.Add(particle);

                var translateX = Math.Cos(angle) * particleRadius;
                var translateY = Math.Sin(angle) * particleRadius;
                animations.Add(
                    AnimateParticleAsync(
                        particle,
                        translateX,
                        translateY,
                        delay + (index % 3 * 18)));
            }
        }

        private static async Task AnimateParticleAsync(
            View particle,
            double translateX,
            double translateY,
            int delay)
        {
            await Task.Delay(delay);
            particle.Opacity = 1;

            await Task.WhenAll(
                particle.TranslateTo(translateX, translateY, 820, Easing.CubicOut),
                particle.ScaleTo(1, 280, Easing.CubicOut),
                particle.RotateTo(180, 820, Easing.Linear),
                particle.FadeTo(0, 940, Easing.CubicIn));
        }
    }
}
