using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp;

public partial class MainWindow : Window
{
    private ObservableCollection<TaskItem> _tasks = new();
    // master list (unfiltered) and filtered view bound to UI
    private List<TaskItem> _allTasks = new();
    private const string DataFolder = "data";
    private const string DataFile = "data/tasks.json";
    private readonly System.Timers.Timer _autosaveTimer;
    private const int AutosaveDelayMs = 1500; // debounce

    public MainWindow()
    {
        InitializeComponent();
        TaskList.ItemsSource = _tasks;

        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        SaveButton.Click += OnSaveClick;
        CompleteAllButton.Click += OnCompleteAllClick;
        ClearCompletedButton.Click += OnClearCompletedClick;
    QuickFilterCombo.SelectionChanged += (s, e) => ApplyFilters();
    TagFilterCombo.SelectionChanged += (s, e) => ApplyFilters();

        // setup autosave debounce timer
        _autosaveTimer = new System.Timers.Timer(AutosaveDelayMs) { AutoReset = false };
        _autosaveTimer.Elapsed += (s, e) => RunSaveInBackground();

    // subscribe to collection change and item property changes to autosave
    _tasks.CollectionChanged += (s, e) => ScheduleAutosave();

        // Charger les tâches au démarrage
        LoadTasks();
    }

    private async void LoadTasks()
    {
        try
        {
            if (File.Exists(DataFile))
            {
                var json = await File.ReadAllTextAsync(DataFile);
                try
                {
                    var tasks = JsonSerializer.Deserialize<List<TaskItem>>(json);
                    if (tasks != null)
                    {
                        _tasks.Clear();
                        _allTasks.Clear();
                        foreach (var task in tasks)
                        {
                            // subscribe to property changes for autosave
                            task.PropertyChanged += (s, ev) => ScheduleAutosave();
                            _allTasks.Add(task);
                            _tasks.Add(task);
                        }
                    }
                }
                catch (JsonException)
                {
                    // corrupted JSON: move to .corrupt and start with empty list
                    var corruptName = DataFile + ".corrupt." + DateTimeOffset.Now.ToUnixTimeSeconds();
                    try { File.Move(DataFile, corruptName); } catch { }
                    await ShowMessageBox("Warning", "tasks.json is corrupted. A backup was created and a new file will be used.");
                }

                RefreshTagFilterItems();
            }
        }
        catch (Exception ex)
        {
            await ShowMessageBox("Error", $"Failed to load tasks: {ex.Message}");
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TaskInput.Text))
        {
            var task = new TaskItem
            {
                Title = TaskInput.Text,
                DueDate = DueDatePicker.SelectedDate,
                Tags = ParseTags(TagsInput.Text)
            };

            // subscribe to property changed for autosave
            task.PropertyChanged += (s, ev) => ScheduleAutosave();

            _tasks.Add(task);
            _allTasks.Add(task);
            TaskInput.Text = string.Empty;
            TagsInput.Text = string.Empty;
            DueDatePicker.SelectedDate = null;
            ScheduleAutosave();
            RefreshTagFilterItems();
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
            _allTasks.Remove(selected);
            ScheduleAutosave();
            RefreshTagFilterItems();
        }
    }

    private void RefreshTagFilterItems()
    {
        var unique = _allTasks.SelectMany(t => t.Tags ?? new List<string>())
                              .Where(x => !string.IsNullOrWhiteSpace(x))
                              .Select(x => x.Trim())
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .OrderBy(x => x)
                              .ToList();

        TagFilterCombo.Items.Clear();
        foreach (var tag in unique)
        {
            TagFilterCombo.Items.Add(new ComboBoxItem { Content = tag });
        }
    }

    private void ApplyFilters()
    {
        // Quick filter
        var quick = (QuickFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
        var tag = (TagFilterCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();

        _tasks.Clear();
        foreach (var t in _allTasks)
        {
            if (!PassesQuickFilter(t, quick))
                continue;
            if (!string.IsNullOrEmpty(tag) && (t.Tags == null || !t.Tags.Contains(tag)))
                continue;
            _tasks.Add(t);
        }
    }

    private bool PassesQuickFilter(TaskItem t, string quick)
    {
        if (quick == "All") return true;
        var now = DateTimeOffset.Now;
        if (quick == "Today")
            return t.DueDate?.Date == now.Date;
        if (quick == "This week")
        {
            var start = now.Date;
            var end = now.Date.AddDays(7);
            return t.DueDate >= start && t.DueDate <= end;
        }
        if (quick == "Overdue")
            return t.DueDate.HasValue && t.DueDate < now && !t.IsCompleted;
        return true;
    }

    private void OnCompleteAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var t in _allTasks)
            t.IsCompleted = true;
        ScheduleAutosave();
    }

    private void OnClearCompletedClick(object? sender, RoutedEventArgs e)
    {
        // Remove from master list
        for (int i = _allTasks.Count - 1; i >= 0; i--)
        {
            if (_allTasks[i].IsCompleted)
                _allTasks.RemoveAt(i);
        }
        // Reapply filters to update UI
        ApplyFilters();
        ScheduleAutosave();
        RefreshTagFilterItems();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveTasksAsync(showMessage: true);
    }

    private async System.Threading.Tasks.Task SaveTasksAsync(bool showMessage = false)
    {
        try
        {
            // Update save status on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SaveStatus.Text = "Saving...");

            if (!Directory.Exists(DataFolder))
                Directory.CreateDirectory(DataFolder);

            // create a backup if file exists
            if (File.Exists(DataFile))
            {
                var bak = DataFile + ".bak";
                try { File.Copy(DataFile, bak, true); } catch { /* continue even if backup fails */ }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_allTasks, options);
            await File.WriteAllTextAsync(DataFile, json);

            // Update save status on UI thread
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SaveStatus.Text = $"Saved at {timestamp}");

            if (showMessage)
                await ShowMessageBox("Success", "Tasks saved successfully!");
        }
        catch (Exception ex)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => SaveStatus.Text = "Save failed!");
            await ShowMessageBox("Error", $"Failed to save tasks: {ex.Message}");
        }
    }

    private void RunSaveInBackground()
    {
        // fire-and-forget save (no UI thread)
        _ = SaveTasksAsync(showMessage: false);
    }

    private void ScheduleAutosave()
    {
        // reset debounce timer
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
    }

    private List<string> ParseTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();
        return input.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
    }

    private void OnTitleDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.IsReadOnly = false;
            textBox.BorderThickness = new Avalonia.Thickness(1);
            textBox.Background = Avalonia.Media.Brushes.White;
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void OnTitleLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.IsReadOnly = true;
            textBox.BorderThickness = new Avalonia.Thickness(0);
            textBox.Background = Avalonia.Media.Brushes.Transparent;
            ScheduleAutosave();
        }
    }

    private async System.Threading.Tasks.Task ShowMessageBox(string title, string message)
    {
        var messageBox = new Window
        {
            Title = title,
            Width = 300,
            Height = 150,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button 
                    { 
                        Content = "OK", 
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Margin = new Avalonia.Thickness(0, 20, 0, 0)
                    }
                }
            }
        };

        var button = ((StackPanel)messageBox.Content).Children[1] as Button;
        if (button != null)
        {
            button.Click += (s, e) => messageBox.Close();
        }

        await messageBox.ShowDialog(this);
    }
}
