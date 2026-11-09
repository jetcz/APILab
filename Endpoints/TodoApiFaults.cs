using APILab.Data;
using APILab.Models;
using Microsoft.AspNetCore.Http;

namespace APILab.Endpoints;

internal static class TodoApiFaults
{
    public static object ToPayload(Todo todo, FeatureFlagStore featureFlags)
    {
        var omitDueBy = featureFlags.IsEnabled(FeatureFlagKeys.OmitResponseFields);
        // INTENTIONAL QA BUG: Bug 2 omits the dueBy property from REST JSON responses.
        var dueBy = todo.DueBy;
        if (featureFlags.IsEnabled(FeatureFlagKeys.AlterDateOffsets) && dueBy is { } date)
        {
            // INTENTIONAL QA BUG: Bug 7 shifts returned due dates by one day without changing stored data.
            dueBy = date.AddDays(1);
        }

        if (!omitDueBy && dueBy == todo.DueBy)
        {
            return todo;
        }

        var payload = new Dictionary<string, object?>
        {
            ["id"] = todo.Id,
            ["title"] = todo.Title,
            ["isComplete"] = todo.IsComplete
        };

        if (!omitDueBy)
        {
            payload["dueBy"] = dueBy;
        }

        return payload;
    }

    public static IResult Missing(FeatureFlagStore featureFlags, string operation, int id)
    {
        if (featureFlags.IsEnabled(FeatureFlagKeys.InconsistentStatusCodes))
        {
            // INTENTIONAL QA BUG: Bug 1 reports a missing resource as a successful response.
            return Results.Ok(new { message = $"Todo {id} was not found." });
        }

        if (featureFlags.IsEnabled(FeatureFlagKeys.InconsistentErrorBodies))
        {
            // INTENTIONAL QA BUG: Bug 5 varies the error envelope by operation.
            return operation switch
            {
                "read" => Results.Json(new { error = "not_found", resourceId = id }, statusCode: StatusCodes.Status404NotFound),
                "update" => Results.Json(new { code = "MISSING_TODO", detail = $"No todo exists with id {id}." }, statusCode: StatusCodes.Status404NotFound),
                _ => Results.Json(new { message = $"Todo {id} could not be deleted." }, statusCode: StatusCodes.Status404NotFound)
            };
        }

        return Results.NotFound();
    }

    public static IResult? ValidateRequest(TodoRequest request, FeatureFlagStore featureFlags)
    {
        if (featureFlags.IsEnabled(FeatureFlagKeys.SkipRequestValidation))
        {
            // INTENTIONAL QA BUG: Bug 8 accepts blank or overlong titles instead of returning a validation error.
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Title) && request.Title.Length <= 120)
        {
            return null;
        }

        if (featureFlags.IsEnabled(FeatureFlagKeys.InconsistentErrorBodies))
        {
            return Results.Json(new { issue = "title_invalid", hint = "Provide a nonblank title of at most 120 characters." }, statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["A nonblank title of at most 120 characters is required."]
        });
    }

    public static object ToInternalDiagnostic()
    {
        // INTENTIONAL QA BUG: Bug 6 exposes a fixed, synthetic implementation diagnostic, never a real stack trace or secret.
        return new
        {
            error = "diagnostic_failure",
            diagnostic = "TodoStore.GetById queried the Todos table; synthetic QA diagnostic."
        };
    }
}