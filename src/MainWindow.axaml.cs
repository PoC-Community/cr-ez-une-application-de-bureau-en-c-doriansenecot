using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using TodoListApp.Models;

namespace TodoListApp;

public partial class MainWindow : Window
{
    private ObservableCollection<TaskItem> _tasks = new();
    private const string DataFolder = "data";
    private const string DataFile = "data/tasks.json";

    public MainWindow()
    {
        InitializeComponent();
        TaskList.ItemsSource = _tasks;

        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        SaveButton.Click += OnSaveClick;

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
            _tasks.Add(new TaskItem { Title = TaskInput.Text });
            TaskInput.Text = string.Empty;
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (TaskList.SelectedItem is TaskItem selected)
        {
            _tasks.Remove(selected);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Créer le dossier data s'il n'existe pas
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }

            // Sérialiser les tâches en JSON
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true 
            };
            var json = JsonSerializer.Serialize(_tasks, options);
            
            // Écrire dans le fichier
            await File.WriteAllTextAsync(DataFile, json);

            // Afficher un message de succès (optionnel)
            await ShowMessageBox("Success", "Tasks saved successfully!");
        }
        catch (Exception ex)
        {
            await ShowMessageBox("Error", $"Failed to save tasks: {ex.Message}");
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
