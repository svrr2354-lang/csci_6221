using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;


namespace SmartScreener.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Browse for resume file
        private void BrowseResume_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Resume files (*.pdf;*.txt)|*.pdf;*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ResumePathTextBox.Text = dialog.FileName;
            }
        }

        // Score resume button
        private void ScoreResume_Click(object sender, RoutedEventArgs e)
        {
            var resumePath = ResumePathTextBox.Text?.Trim();
            var jdTitle = JobTitleTextBox.Text?.Trim();
            var jdText = JobDescriptionTextBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(resumePath))
            {
                MessageBox.Show("Please select a resume file.", "Missing resume",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(jdTitle))
            {
                MessageBox.Show("Please enter a job title.", "Missing job title",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(jdText))
            {
                MessageBox.Show("Please paste a job description.", "Missing job description",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Call F# extraction
                var resume = SmartScreener.Core.Extract.loadResume(resumePath);

                // F# record: type JobDescription = { Title : string; Text : string }
                var jd = new SmartScreener.Core.JobDescription(jdTitle, jdText);

                // F# interface and implementation in MatchEngine
                SmartScreener.Core.MatchEngine.IScorer scorer =
                    new SmartScreener.Core.MatchEngine.TfidfScorer();

                var result = scorer.Score(resume, jd);

                // Show score
                ScoreTextBlock.Text = result.Score.ToString("0.0000");

                // Show overlapping terms
                var overlapItems =
                    result.TopOverlap
                          .Select(t => $"{t.Term}   (r={t.ResumeTfIdf:F3}, jd={t.JdTfIdf:F3})")
                          .ToList();

                OverlapListBox.ItemsSource = overlapItems;

                // Show resume preview
                ResumePreviewTextBox.Text = result.ResumePreview;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while scoring resume:\n" + ex.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
    }
}
