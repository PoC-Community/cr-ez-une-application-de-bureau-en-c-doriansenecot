using System;

namespace TodoListApp.Models;

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
}
