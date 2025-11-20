using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SmartScreener.UI
{
    public partial class MainWindow : Window
    {
        private object? _lastResult;
        private string? _lastResumePath;
        private string? _lastJdTitle;
        private string? _lastJdText;

        public MainWindow()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            InitializeComponent();
        }

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

        private void ScoreResume_Click(object sender, RoutedEventArgs e)
        {
            var resumePath = ResumePathTextBox.Text?.Trim();
            var jdTitle = JobTitleTextBox.Text?.Trim();
            var jdText = JobDescriptionTextBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(resumePath))
            {
                MessageBox.Show("Please select a resume file.");
                return;
            }

            if (string.IsNullOrWhiteSpace(jdTitle))
            {
                MessageBox.Show("Please enter a job title.");
                return;
            }

            if (string.IsNullOrWhiteSpace(jdText))
            {
                MessageBox.Show("Please paste the job description.");
                return;
            }

            try
            {
                // Call into F# core
                var resume = SmartScreener.Core.Extract.loadResume(resumePath);
                var jd = new SmartScreener.Core.JobDescription(jdTitle, jdText);

                SmartScreener.Core.MatchEngine.IScorer scorer =
                    new SmartScreener.Core.MatchEngine.TfidfScorer();

                var result = scorer.Score(resume, jd);


                _lastResult = result;
                _lastResumePath = resumePath;
                _lastJdTitle = jdTitle;
                _lastJdText = jdText;

           
                dynamic r = result;

                
                double score = (double)r.Score;
                ScoreTextBlock.Text = score.ToString("0.0000");

                
                var overlapItems =
                    ((System.Collections.IEnumerable)r.TopOverlap)
                    .Cast<dynamic>()
                    .Select(t => $"{(string)t.Term} (r={(double)t.ResumeTfIdf:F3}, jd={(double)t.JdTfIdf:F3})")
                    .ToList();

                OverlapListBox.ItemsSource = overlapItems;

                
                ResumePreviewTextBox.Text = (string)r.ResumePreview;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error scoring resume:\n{ex.Message}");
            }
        }

        
        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult == null ||
                string.IsNullOrWhiteSpace(_lastResumePath) ||
                string.IsNullOrWhiteSpace(_lastJdTitle) ||
                string.IsNullOrWhiteSpace(_lastJdText))
            {
                MessageBox.Show("Please score a resume first, then export.",
                    "No data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = "ResumeReport.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    GeneratePdfReport(dialog.FileName);
                    MessageBox.Show("PDF report saved:\n" + dialog.FileName,
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save PDF:\n" + ex.Message,
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GeneratePdfReport(string filePath)
        {
            if (_lastResult == null ||
                _lastResumePath == null ||
                _lastJdTitle == null ||
                _lastJdText == null)
            {
                throw new InvalidOperationException("No scoring result available.");
            }

            dynamic r = _lastResult;

            double score = (double)r.Score;
            string resumePreview = (string)r.ResumePreview;

            string overlapsText = "";
            foreach (var item in (System.Collections.IEnumerable)r.TopOverlap)
            {
                dynamic t = item;
                string term = (string)t.Term;
                double rtf = (double)t.ResumeTfIdf;
                double jtf = (double)t.JdTfIdf;

                overlapsText += $"• {term,-20} r={rtf:F3}  jd={jtf:F3}\n";
            }

            Document
                .Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);

                        page.Content().Column(col =>
                        {
                            col.Spacing(10);

                            col.Item().Text("Smart Resume Screener Report")
                                .FontSize(20).SemiBold();

                            col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                                .FontSize(10).FontColor(Colors.Grey.Darken2);

                            col.Item().Text($"Resume: {_lastResumePath}")
                                .FontSize(10);

                            col.Item().Text($"Job Title: {_lastJdTitle}")
                                .FontSize(11);

                            col.Item().Text($"Match Score: {score:0.0000}")
                                .FontSize(12).SemiBold().FontColor(Colors.Blue.Medium);

                            col.Item().Text("Top Overlapping Terms:")
                                .FontSize(12).SemiBold();

                            col.Item().Text(overlapsText)
                                .FontSize(10);

                            col.Item().Text("Resume Preview:")
                                .FontSize(12).SemiBold();

                            col.Item().Text(resumePreview)
                                .FontSize(10);

                            col.Item().Text("Job Description:")
                                .FontSize(12).SemiBold();

                            col.Item().Text(_lastJdText)
                                .FontSize(10);
                        });
                    });
                })
                .GeneratePdf(filePath);
        }
    }
}
