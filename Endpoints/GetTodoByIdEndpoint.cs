using APILab.Data;
using APILab.Models;
using Microsoft.AspNetCore.Builder;

namespace APILab.Endpoints;

public static class GetTodoByIdEndpoint
{
    public static RouteHandlerBuilder MapGetTodoById(this RouteGroupBuilder endpoints) =>
        endpoints.MapGet("/{id}", (int id, TodoStore store, FeatureFlagStore featureFlags) =>
        {
            if (id < 0 && featureFlags.IsEnabled(FeatureFlagKeys.ExposeInternalDetails))
            {
                // INTENTIONAL QA BUG: Bug 6 returns a synthetic diagnostic for a controlled invalid-ID request.
                return Results.Json(TodoApiFaults.ToInternalDiagnostic(), statusCode: StatusCodes.Status500InternalServerError);
            }

            return store.GetById(id) is { } todo
                ? Results.Ok(TodoApiFaults.ToPayload(todo, featureFlags))
                : TodoApiFaults.Missing(featureFlags, "read", id);
        })
            .WithName("GetTodoById");
}