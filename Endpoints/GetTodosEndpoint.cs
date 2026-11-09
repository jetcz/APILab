using APILab.Data;
using APILab.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace APILab.Endpoints;

public static class GetTodosEndpoint
{
    public static RouteHandlerBuilder MapGetTodos(this RouteGroupBuilder endpoints) =>
        endpoints.MapGet("/", (TodoStore store, FeatureFlagStore featureFlags, string? title, bool? isComplete, string? sortBy, int? page, int? pageSize) =>
        {
            var result = store.GetPage(page ?? 1, pageSize ?? 10, title, isComplete, sortBy);
            var items = result.Items
                .Select(todo => TodoApiFaults.ToPayload(todo, featureFlags))
                .ToArray();
            var totalCount = result.TotalCount;
            var totalPages = result.TotalPages;
            var currentPage = result.Page;
            var currentPageSize = result.PageSize;

            if (featureFlags.IsEnabled(FeatureFlagKeys.IncorrectPaginationMetadata))
            {
                // INTENTIONAL QA BUG: Bug 4 makes pagination totals disagree with the returned collection.
                totalCount++;
                totalPages++;
            }

            return Results.Ok(new { items, page = currentPage, pageSize = currentPageSize, totalCount, totalPages });
        })
            .WithName("GetTodos");

    public static RouteHandlerBuilder MapCreateTodo(this RouteGroupBuilder endpoints) =>
        endpoints.MapPost("/", (TodoRequest request, TodoStore store, FeatureFlagStore featureFlags) =>
        {
            var validationResult = TodoApiFaults.ValidateRequest(request, featureFlags);
            if (validationResult is not null)
            {
                return validationResult;
            }

            var todo = store.Create(request.Title, request.DueBy, request.IsComplete);
            var payload = TodoApiFaults.ToPayload(todo, featureFlags);
            if (featureFlags.IsEnabled(FeatureFlagKeys.InconsistentStatusCodes))
            {
                // INTENTIONAL QA BUG: Bug 1 returns 200 instead of 201 for creates.
                return Results.Ok(payload);
            }

            return Results.CreatedAtRoute("GetTodoById", new RouteValueDictionary { ["id"] = todo.Id }, payload);
        })
        .WithName("CreateTodo");

    public static RouteHandlerBuilder MapUpdateTodo(this RouteGroupBuilder endpoints) =>
        endpoints.MapPut("/{id}", (int id, TodoRequest request, TodoStore store, FeatureFlagStore featureFlags) =>
        {
            var validationResult = TodoApiFaults.ValidateRequest(request, featureFlags);
            if (validationResult is not null)
            {
                return validationResult;
            }

            return store.Update(id, request.Title, request.DueBy, request.IsComplete) is { } todo
                ? Results.Ok(TodoApiFaults.ToPayload(todo, featureFlags))
                : TodoApiFaults.Missing(featureFlags, "update", id);
        })
        .WithName("UpdateTodo");

    public static RouteHandlerBuilder MapDeleteTodo(this RouteGroupBuilder endpoints) =>
        endpoints.MapDelete("/{id}", (int id, TodoStore store, FeatureFlagStore featureFlags) =>
            store.Delete(id)
                ? Results.NoContent()
                : TodoApiFaults.Missing(featureFlags, "delete", id))
        .WithName("DeleteTodo");
}