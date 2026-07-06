using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace InfinitariumManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _filePath = "";
        private string _originalHtml = "";

        public string[] SectionTypes { get; } = { "Additions", "Improvements", "Bug Fixes", "Custom" };

        public class VersionItem : INotifyPropertyChanged
        {
            private string _id = "";
            private string _label = "";
            private bool _isUnreleased;

            public string Id
            {
                get => _id;
                set { _id = value; OnPropertyChanged(nameof(Id)); OnPropertyChanged(nameof(VersionTag)); }
            }
            public string Label
            {
                get => _label;
                set { _label = value; OnPropertyChanged(nameof(Label)); OnPropertyChanged(nameof(ButtonText)); }
            }
            public ObservableCollection<ChangelogSection> Sections { get; set; } = new ObservableCollection<ChangelogSection>();
            public bool IsUnreleased
            {
                get => _isUnreleased;
                set { _isUnreleased = value; OnPropertyChanged(nameof(IsUnreleased)); }
            }

            public string VersionTag => Id;
            public string ButtonText => Label;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class ChangelogSection : INotifyPropertyChanged
        {
            private string _type = "Additions";
            private string _customTitle = "";
            private string _itemsText = "";

            public string Type
            {
                get => _type;
                set { _type = value; OnPropertyChanged(nameof(Type)); OnPropertyChanged(nameof(IsCustomVisible)); }
            }
            public string CustomTitle
            {
                get => _customTitle;
                set { _customTitle = value; OnPropertyChanged(nameof(CustomTitle)); }
            }
            public string ItemsText
            {
                get => _itemsText;
                set { _itemsText = value; OnPropertyChanged(nameof(ItemsText)); }
            }

            public Visibility IsCustomVisible => _type == "Custom" ? Visibility.Visible : Visibility.Collapsed;

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private ObservableCollection<VersionItem> _versions = new ObservableCollection<VersionItem>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            LstVersions.ItemsSource = _versions;
            ListSections.ItemsSource = new ObservableCollection<ChangelogSection>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "HTML Files|*.html", Title = "Выберите файл" };
            if (dialog.ShowDialog() == true)
            {
                _filePath = dialog.FileName;
                LblFilePath.Text = System.IO.Path.GetFileName(_filePath);
                BtnSave.IsEnabled = true;
                try
                {
                    _originalHtml = File.ReadAllText(_filePath);
                    ParseHtml();
                    LblStatus.Text = $"Загружено {_versions.Count} версий.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ParseHtml()
        {
            _versions.Clear();

            var buttonPattern = new Regex(@"<button\s+[^>]*?class=""tab-btn(.*?)""[^>]*?>", RegexOptions.Singleline);
            var buttonMatches = buttonPattern.Matches(_originalHtml);

            foreach (Match match in buttonMatches)
            {
                string fullButtonTag = match.Value;
                string classes = match.Groups[1].Value;
                bool isUnreleased = classes.Contains("unreleased");

                var dataTabMatch = Regex.Match(fullButtonTag, @"data-tab=""(.*?)""");
                if (!dataTabMatch.Success) continue;
                string id = dataTabMatch.Groups[1].Value;

                int tagEnd = match.Index + match.Length;
                int closeIndex = _originalHtml.IndexOf("</button>", tagEnd);
                if (closeIndex < 0) continue;
                string label = _originalHtml.Substring(tagEnd, closeIndex - tagEnd).Trim();

                string contentPattern = $@"<div\s+[^>]*?id=""{Regex.Escape(id)}""[^>]*?class=""tab-content[^""]*""[^>]*?>([\s\S]*?)</div>\s*(?=<div\s+[^>]*?id=""|</div>\s*<button)";
                Match contentMatch = Regex.Match(_originalHtml, contentPattern);

                string rawContent = "";
                if (contentMatch.Success)
                {
                    rawContent = contentMatch.Groups[1].Value.Trim();
                }

                var sections = HtmlToSections(rawContent);

                _versions.Add(new VersionItem
                {
                    Id = id,
                    Label = label,
                    Sections = sections,
                    IsUnreleased = isUnreleased
                });
            }
        }

        private ObservableCollection<ChangelogSection> HtmlToSections(string html)
        {
            var sections = new ObservableCollection<ChangelogSection>();

            string sectionPattern = @"<h3>(.*?)</h3>\s*<ul\s+class=""changelog-list"">(.*?)</ul>";
            MatchCollection matches = Regex.Matches(html, sectionPattern, RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                string title = m.Groups[1].Value.Trim();
                string listHtml = m.Groups[2].Value.Trim();

                var items = new List<string>();
                MatchCollection liMatches = Regex.Matches(listHtml, @"<li>(.*?)</li>", RegexOptions.Singleline);
                foreach (Match li in liMatches)
                {
                    items.Add(li.Groups[1].Value.Trim());
                }

                var section = new ChangelogSection();
                section.ItemsText = string.Join(Environment.NewLine, items);

                string normalizedTitle = title.TrimEnd(':').Trim();

                if (normalizedTitle.Equals("Additions", StringComparison.OrdinalIgnoreCase))
                    section.Type = "Additions";
                else if (normalizedTitle.Equals("Improvements", StringComparison.OrdinalIgnoreCase))
                    section.Type = "Improvements";
                else if (normalizedTitle.Equals("Bug Fixes", StringComparison.OrdinalIgnoreCase))
                    section.Type = "Bug Fixes";
                else
                {
                    section.Type = "Custom";
                    section.CustomTitle = normalizedTitle;
                }

                sections.Add(section);
            }

            return sections;
        }

        private void LstVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstVersions.SelectedItem is VersionItem item)
            {
                TxtId.Text = item.Id;
                TxtButtonLabel.Text = item.Label;
                ChkUnreleased.IsChecked = item.IsUnreleased;
                ListSections.ItemsSource = item.Sections;
            }
            else
            {
                ClearEditor();
            }
        }

        private void BtnAddSection_Click(object sender, RoutedEventArgs e)
        {
            if (ListSections.ItemsSource is ObservableCollection<ChangelogSection> collection)
            {
                collection.Add(new ChangelogSection { Type = "Additions", ItemsText = "" });
            }
        }

        private void BtnRemoveSection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ChangelogSection section)
            {
                if (ListSections.ItemsSource is ObservableCollection<ChangelogSection> collection)
                {
                    collection.Remove(section);
                }
            }
        }

        private void BtnUpdateVersion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtId.Text))
            {
                MessageBox.Show("Введите ID версии!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string id = TxtId.Text.Trim();
            string label = TxtButtonLabel.Text.Trim();
            bool isUnreleased = ChkUnreleased.IsChecked == true;

            var currentSections = ListSections.ItemsSource as ObservableCollection<ChangelogSection>;
            if (currentSections == null) currentSections = new ObservableCollection<ChangelogSection>();

            var existing = _versions.FirstOrDefault(v => v.Id == id);

            if (existing != null)
            {
                existing.Label = label;
                existing.Sections = currentSections;
                existing.IsUnreleased = isUnreleased;
                LblStatus.Text = $"Версия {id} обновлена.";
            }
            else
            {
                var newItem = new VersionItem
                {
                    Id = id,
                    Label = label,
                    Sections = new ObservableCollection<ChangelogSection>(currentSections),
                    IsUnreleased = isUnreleased
                };
                _versions.Insert(0, newItem);
                LstVersions.SelectedItem = newItem;
                LblStatus.Text = $"Версия {id} добавлена.";
            }
        }

        private void BtnNewVersion_Click(object sender, RoutedEventArgs e)
        {
            LstVersions.SelectedIndex = -1;
            ClearEditor();
            LblStatus.Text = "Готов к созданию новой версии.";
        }

        private void BtnDeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (LstVersions.SelectedItem is VersionItem item)
            {
                if (MessageBox.Show($"Удалить версию {item.Id}?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _versions.Remove(item);
                    ClearEditor();
                    LblStatus.Text = $"Версия {item.Id} удалена.";
                }
            }
            else
            {
                MessageBox.Show("Сначала выберите версию для удаления.", "Подсказка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearEditor()
        {
            TxtId.Clear();
            TxtButtonLabel.Clear();
            ChkUnreleased.IsChecked = false;
            ListSections.ItemsSource = new ObservableCollection<ChangelogSection>();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_filePath)) return;

            try
            {
                string newTabsHtml = "";
                foreach (var v in _versions)
                {
                    string unreleasedClass = v.IsUnreleased ? " unreleased" : "";
                    newTabsHtml += $@"                    <button class=""tab-btn{unreleasedClass}"" data-tab=""{v.Id}"">{v.Label}</button>" + Environment.NewLine;
                }

                string newContentHtml = "";
                foreach (var v in _versions)
                {
                    string activeClass = (v == _versions[0]) ? " active" : "";

                    newContentHtml += $@"                    <div id=""{v.Id}"" class=""tab-content{activeClass}"">" + Environment.NewLine;

                    foreach (var sec in v.Sections)
                    {
                        string title = sec.Type == "Custom" ? sec.CustomTitle + ":" : sec.Type + ":";

                        string itemsHtml = "";
                        if (!string.IsNullOrWhiteSpace(sec.ItemsText))
                        {
                            var lines = sec.ItemsText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                string trimmed = line.Trim();
                                if (!string.IsNullOrEmpty(trimmed))
                                    itemsHtml += $"<li>{trimmed}</li>" + Environment.NewLine;
                            }
                        }

                        newContentHtml += $@"                        <div class=""changelog-section"">" + Environment.NewLine;
                        newContentHtml += $@"                            <h3>{title}</h3>" + Environment.NewLine;
                        newContentHtml += $@"                            <ul class=""changelog-list"">" + Environment.NewLine;
                        newContentHtml += itemsHtml;
                        newContentHtml += $@"                            </ul>" + Environment.NewLine;
                        newContentHtml += $@"                        </div>" + Environment.NewLine;
                    }

                    newContentHtml += $@"                    </div>" + Environment.NewLine;
                }

                string tabsPattern = @"(<div class=""changelog-tabs"">)([\s\S]*?)(</div>)";
                string updatedHtml = Regex.Replace(_originalHtml, tabsPattern, m =>
                    m.Groups[1].Value + Environment.NewLine + newTabsHtml + "                " + m.Groups[3].Value,
                    RegexOptions.Singleline);

                string contentPattern = @"(<div class=""changelog-content-area"">)[\s\S]*(</div>\s*<button class=""close-modal"")";
                updatedHtml = Regex.Replace(updatedHtml, contentPattern, m =>
                    m.Groups[1].Value + Environment.NewLine + newContentHtml + "                " + m.Groups[2].Value,
                    RegexOptions.Singleline);

                File.WriteAllText(_filePath, updatedHtml);
                MessageBox.Show("Сохранено успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LblStatus.Text = "Файл сохранён.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}