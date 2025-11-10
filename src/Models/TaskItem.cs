using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TodoListApp.Models;

public class TaskItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _id = Guid.NewGuid().ToString();
    public string Id { get => _id; set => SetField(ref _id, value); }

    private string _title = string.Empty;
    public string Title { get => _title; set => SetField(ref _title, value); }

    private bool _isCompleted = false;
    public bool IsCompleted { get => _isCompleted; set => SetField(ref _isCompleted, value); }

    // Nullable due date for the task (DateTimeOffset matches Avalonia DatePicker.SelectedDate)
    private DateTimeOffset? _dueDate;
    public DateTimeOffset? DueDate { get => _dueDate; set => SetField(ref _dueDate, value); }

    // Tags as a simple list of strings
    private List<string> _tags = new();
    public List<string> Tags { get => _tags; set => SetField(ref _tags, value); }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
