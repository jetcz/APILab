using APILab.Data;
using APILab.Models;
using System.Globalization;

namespace APILab.Services.Soap;

public sealed class TodoSoapService(TodoStore store, FeatureFlagStore featureFlags) : ITodoSoapService
{
    public SoapTodo[] GetTodos() => store.GetAll().Select(ToResponse).ToArray();

    public SoapTodo? GetTodoById(int id) => store.GetById(id) is { } todo
        ? ToResponse(todo)
        : null;

    public SoapTodo CreateTodo(SoapTodo todo)
    {
        ValidateTitle(todo.Title);
        return ToResponse(store.Create(todo.Title, todo.ToTodo().DueBy, todo.IsComplete));
    }

    public SoapTodo? UpdateTodo(int id, SoapTodo todo)
    {
        ValidateTitle(todo.Title);
        return store.Update(id, todo.Title, todo.ToTodo().DueBy, todo.IsComplete) is { } updated
            ? ToResponse(updated)
            : null;
    }

    public bool DeleteTodo(int id) => store.Delete(id);

    private SoapTodo ToResponse(Todo todo)
    {
        var dueBy = todo.DueBy;
        if (featureFlags.IsEnabled(FeatureFlagKeys.AlterDateOffsets) && dueBy is { } date)
        {
            // INTENTIONAL QA BUG: Bug 7 shifts returned due dates by one day without changing stored data.
            dueBy = date.AddDays(1);
        }

        var response = SoapTodo.FromTodo(todo with { DueBy = dueBy });
        if (featureFlags.IsEnabled(FeatureFlagKeys.OmitResponseFields))
        {
            // INTENTIONAL QA BUG: Bug 2 omits the due-date field from SOAP responses.
            response.DueBy = null;
        }

        return response;
    }

    private void ValidateTitle(string? title)
    {
        if (featureFlags.IsEnabled(FeatureFlagKeys.SkipRequestValidation))
        {
            // INTENTIONAL QA BUG: Bug 8 bypasses the SOAP title validation too.
            return;
        }

        if (string.IsNullOrWhiteSpace(title) || title.Length > 120)
        {
            throw new ArgumentException("A nonblank title of at most 120 characters is required.", nameof(title));
        }
    }
}