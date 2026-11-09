namespace APILab.Models;

public sealed record TodoPage(Todo[] Items, int Page, int PageSize, int TotalCount, int TotalPages);