using System.Collections.ObjectModel;

namespace GermanToolbox
{
    public partial class SearchTab : ContentView
    {
        private readonly ObservableCollection<SearchResultItem> results = [];
        private readonly WordRepository repository;
        private int searchVersion;

        public SearchTab()
        {
            InitializeComponent();
            repository = AppServices.GetRequiredService<WordRepository>();
            ResultsCollection.ItemsSource = results;
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var query = e.NewTextValue?.Trim() ?? string.Empty;
            var version = Interlocked.Increment(ref searchVersion);

            if (query.Length < 2)
            {
                results.Clear();
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                StatusLabel.Text = "Type at least 2 letters to start searching.";
                ShowEmptyState(
                    "Wortliste durchsuchen",
                    "Results will appear here as soon as you enter two letters.");
                return;
            }

            LoadingIndicator.IsRunning = true;
            LoadingIndicator.IsVisible = true;
            StatusLabel.Text = "Suche läuft...";

            await Task.Delay(180);
            if (version != searchVersion)
            {
                return;
            }

            try
            {
                var matches = await repository.SearchWordsAsync(query, maximumCount: 25);
                if (version != searchVersion)
                {
                    return;
                }

                results.Clear();
                foreach (var match in matches)
                {
                    results.Add(new SearchResultItem(match));
                }

                StatusLabel.Text = matches.Count switch
                {
                    0 => "Keine Treffer",
                    1 => "1 Treffer",
                    _ => $"{matches.Count} Treffer"
                };

                if (matches.Count == 0)
                {
                    ShowEmptyState(
                        "Keine passenden Wörter",
                        "Try another German word or English translation.");
                }
                else
                {
                    EmptyState.IsVisible = false;
                }
            }
            catch (Exception)
            {
                if (version == searchVersion)
                {
                    results.Clear();
                    StatusLabel.Text = "Suche nicht verfügbar";
                    ShowEmptyState(
                        "Suche konnte nicht abgeschlossen werden",
                        "Please try again in a moment.");
                }
            }
            finally
            {
                if (version == searchVersion)
                {
                    LoadingIndicator.IsRunning = false;
                    LoadingIndicator.IsVisible = false;
                }
            }
        }

        private async void OnResultTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not SearchResultItem item)
            {
                return;
            }

            await Shell.Current.GoToAsync(
                $"{nameof(WordDetailsPage)}?wordId={item.Id}");
        }

        private void ShowEmptyState(string title, string subtitle)
        {
            EmptyTitleLabel.Text = title;
            EmptySubtitleLabel.Text = subtitle;
            EmptyState.IsVisible = true;
        }
    }
}
