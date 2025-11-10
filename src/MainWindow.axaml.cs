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
                var tasks = JsonSerializer.Deserialize<ObservableCollection<TaskItem>>(json);
                
                if (tasks != null)
                {
                    _tasks.Clear();
                    foreach (var task in tasks)
                    {
                        // subscribe to property changes for autosave
                        task.PropertyChanged += (s, ev) => ScheduleAutosave();
                        _tasks.Add(task);
                    }
                }
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
            TaskInput.Text = string.Empty;
            TagsInput.Text = string.Empty;
            DueDatePicker.SelectedDate = null;
            ScheduleAutosave();
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
            ScheduleAutosave();
        }
    }

    private void OnCompleteAllClick(object? sender, RoutedEventArgs e)
    {
        foreach (var t in _tasks)
            t.IsCompleted = true;
        ScheduleAutosave();
    }

    private void OnClearCompletedClick(object? sender, RoutedEventArgs e)
    {
        for (int i = _tasks.Count - 1; i >= 0; i--)
        {
            if (_tasks[i].IsCompleted)
                _tasks.RemoveAt(i);
        }
        ScheduleAutosave();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        await SaveTasksAsync(showMessage: true);
    }

    private async System.Threading.Tasks.Task SaveTasksAsync(bool showMessage = false)
    {
        try
        {
            if (!Directory.Exists(DataFolder))
                Directory.CreateDirectory(DataFolder);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_tasks, options);
            await File.WriteAllTextAsync(DataFile, json);

            if (showMessage)
                await ShowMessageBox("Success", "Tasks saved successfully!");
        }
        catch (Exception ex)
        {
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
