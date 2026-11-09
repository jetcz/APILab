namespace APILab.Models;

public sealed record TodoRequest(string? Title, DateOnly? DueBy = null, bool IsComplete = false);