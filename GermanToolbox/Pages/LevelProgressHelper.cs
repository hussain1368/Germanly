using Microsoft.Maui.Controls;
using Microsoft.Maui.Layouts;

namespace GermanToolbox
{
    internal static class LevelProgressHelper
    {
        private const double ProgressStep = 0.05d;

        public static readonly string[] Levels = ["A1", "A2", "B1", "B2", "C1"];

        public static async Task<IReadOnlyDictionary<string, PracticeModeSummary>> GetSummariesAsync(
            Func<string, Task<PracticeModeSummary>> loadSummaryAsync)
        {
            var summaries = new Dictionary<string, PracticeModeSummary>(StringComparer.Ordinal);
            foreach (var level in Levels)
            {
                summaries[level] = await loadSummaryAsync(level);
            }

            return summaries;
        }

        public static void Apply(AbsoluteLayout progressLayout, PracticeModeSummary summary)
        {
            ApplyProgress(progressLayout, summary.Progress);
        }

        private static void ApplyProgress(AbsoluteLayout progressLayout, double progress)
        {
            if (progressLayout.Parent is Grid parentGrid)
            {
                parentGrid.HorizontalOptions = LayoutOptions.Fill;
                parentGrid.VerticalOptions = LayoutOptions.Fill;
            }

            progressLayout.HorizontalOptions = LayoutOptions.Fill;
            progressLayout.VerticalOptions = LayoutOptions.Fill;
            if (progressLayout.Children.Count == 0 ||
                progressLayout.Children[0] is not View fillView)
            {
                return;
            }

            var displayProgress = GetDisplayProgress(progress);
            AbsoluteLayout.SetLayoutFlags(fillView, AbsoluteLayoutFlags.All);
            AbsoluteLayout.SetLayoutBounds(fillView, new Rect(0, 0, displayProgress, 1));
        }

        private static double GetDisplayProgress(double progress)
        {
            var clampedProgress = Math.Clamp(progress, 0, 1);
            if (clampedProgress >= 1)
            {
                return 1;
            }

            var stepCount = Math.Floor((clampedProgress * 100) / (ProgressStep * 100));
            return stepCount * ProgressStep;
        }
    }
}
