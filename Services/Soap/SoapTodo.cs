using APILab.Models;
using System.Globalization;

namespace APILab.Services.Soap;

public sealed class SoapTodo
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? DueBy { get; set; }
    public bool IsComplete { get; set; }

    public bool ShouldSerializeDueBy() => DueBy is not null;

    public static SoapTodo FromTodo(Todo todo) => new()
    {
        Id = todo.Id,
        Title = todo.Title,
        DueBy = todo.DueBy?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IsComplete = todo.IsComplete
    };

    public Todo ToTodo() => new(
        Id,
        Title,
        string.IsNullOrEmpty(DueBy)
            ? null
            : DateOnly.ParseExact(DueBy, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        IsComplete);
}