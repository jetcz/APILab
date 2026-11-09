using APILab.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace APILab.Data;

public sealed class TodoStore
{
    private readonly string connectionString;
    private readonly FeatureFlagStore featureFlags;
    private readonly object staleReadLock = new();
    private readonly object recentUpdateLock = new();
    private readonly Dictionary<int, DateTime> recentUpdates = [];
    private Todo[]? staleReadSnapshot;

    public TodoStore(string databasePath, Todo[] seedTodos, FeatureFlagStore featureFlags)
    {
        this.featureFlags = featureFlags;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

        using var connection = OpenConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Todos (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NULL,
                    DueBy TEXT NULL,
                    IsComplete INTEGER NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Todos;";
        if ((long)countCommand.ExecuteScalar()! != 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        using var seedCommand = connection.CreateCommand();
        seedCommand.Transaction = transaction;
        seedCommand.CommandText = """
            INSERT INTO Todos (Id, Title, DueBy, IsComplete)
            VALUES ($id, $title, $dueBy, $isComplete);
            """;
        var idParameter = seedCommand.Parameters.Add("$id", SqliteType.Integer);
        var titleParameter = seedCommand.Parameters.Add("$title", SqliteType.Text);
        var dueByParameter = seedCommand.Parameters.Add("$dueBy", SqliteType.Text);
        var isCompleteParameter = seedCommand.Parameters.Add("$isComplete", SqliteType.Integer);

        foreach (var todo in seedTodos)
        {
            idParameter.Value = todo.Id;
            titleParameter.Value = (object?)todo.Title ?? DBNull.Value;
            dueByParameter.Value = todo.DueBy?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value;
            isCompleteParameter.Value = todo.IsComplete ? 1 : 0;
            seedCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public Todo[] GetAll(string? title = null, bool? isComplete = null, string? sortBy = null)
    {
        var todos = GetReadSnapshot();
        if (featureFlags.IsEnabled(FeatureFlagKeys.IgnoreQueryParameters))
        {
            // INTENTIONAL QA BUG: Bug 3 ignores filters and sorting even though the endpoint accepts them.
            return todos;
        }

        var comparison = featureFlags.IsEnabled(FeatureFlagKeys.CaseSensitiveMatching)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (!string.IsNullOrWhiteSpace(title))
        {
            // INTENTIONAL QA BUG: Bug 13 changes matching from case-insensitive to case-sensitive.
            todos = todos.Where(todo => todo.Title?.Contains(title, comparison) == true).ToArray();
        }

        if (isComplete is { } completed)
        {
            todos = todos.Where(todo => todo.IsComplete == completed).ToArray();
        }

        return sortBy?.ToLowerInvariant() switch
        {
            "title" => todos.OrderBy(todo => todo.Title, featureFlags.IsEnabled(FeatureFlagKeys.CaseSensitiveMatching)
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase).ToArray(),
            "dueby" => todos.OrderBy(todo => todo.DueBy).ToArray(),
            _ => todos
        };
    }

    public TodoPage GetPage(int page = 1, int pageSize = 10, string? title = null, bool? isComplete = null, string? sortBy = null)
    {
        var currentPage = Math.Max(page, 1);
        var currentPageSize = Math.Clamp(pageSize, 1, 100);
        var todos = GetAll(title, isComplete, sortBy);
        var totalCount = todos.Length;
        var totalPages = (int)Math.Ceiling(totalCount / (double)currentPageSize);
        var offset = (int)Math.Min((long)(currentPage - 1) * currentPageSize, totalCount);
        var items = todos.Skip(offset)
            .Take(currentPageSize)
            .ToArray();

        return new TodoPage(items, currentPage, currentPageSize, totalCount, totalPages);
    }

    private Todo[] GetReadSnapshot()
    {
        if (!featureFlags.IsEnabled(FeatureFlagKeys.StaleReads))
        {
            lock (staleReadLock)
            {
                staleReadSnapshot = null;
            }

            return ReadAllFromDatabase();
        }

        lock (staleReadLock)
        {
            // INTENTIONAL QA BUG: Bug 9 freezes the first read until the flag is turned off.
            staleReadSnapshot ??= ReadAllFromDatabase();
            return staleReadSnapshot;
        }
    }

    private Todo[] ReadAllFromDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title, DueBy, IsComplete FROM Todos ORDER BY Id;";
        using var reader = command.ExecuteReader();
        var todos = new List<Todo>();
        while (reader.Read())
        {
            todos.Add(ReadTodo(reader));
        }

        return todos.ToArray();
    }

    public Todo? GetById(int id)
    {
        return GetAll().FirstOrDefault(todo => todo.Id == id);
    }

    public Todo Create(string? title, DateOnly? dueBy, bool isComplete)
    {
        // INTENTIONAL QA BUG: Bug 14 truncates titles during persistence.
        var storedTitle = featureFlags.IsEnabled(FeatureFlagKeys.TruncateStoredTitles) && title is { Length: > 12 }
            ? title[..12]
            : title;

        var todo = InsertTodo(storedTitle, dueBy, isComplete);
        if (featureFlags.IsEnabled(FeatureFlagKeys.DuplicateWritesOnRetry))
        {
            // INTENTIONAL QA BUG: Bug 10 writes a duplicate record for each create request.
            InsertTodo(storedTitle, dueBy, isComplete);
        }

        return todo;
    }

    private Todo InsertTodo(string? title, DateOnly? dueBy, bool isComplete)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Todos (Title, DueBy, IsComplete)
            VALUES ($title, $dueBy, $isComplete);
            """;
        command.Parameters.AddWithValue("$title", (object?)title ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueBy", dueBy?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$isComplete", isComplete ? 1 : 0);
        command.ExecuteNonQuery();
        using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var id = checked((int)(long)idCommand.ExecuteScalar()!);
        return new Todo(id, title, dueBy, isComplete);
    }

    public Todo? Update(int id, string? title, DateOnly? dueBy, bool isComplete)
    {
        if (featureFlags.IsEnabled(FeatureFlagKeys.LoseConcurrentUpdates))
        {
            var now = DateTime.UtcNow;
            var updateWasDiscarded = false;
            lock (recentUpdateLock)
            {
                updateWasDiscarded = recentUpdates.TryGetValue(id, out var lastUpdate)
                    && now - lastUpdate < TimeSpan.FromMilliseconds(500);
                recentUpdates[id] = now;
            }

            if (updateWasDiscarded)
            {
                // INTENTIONAL QA BUG: Bug 11 silently drops a second update to the same item within 500 ms.
                return GetById(id);
            }
        }

        // INTENTIONAL QA BUG: Bug 14 truncates titles during persistence.
        var storedTitle = featureFlags.IsEnabled(FeatureFlagKeys.TruncateStoredTitles) && title is { Length: > 12 }
            ? title[..12]
            : title;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = featureFlags.IsEnabled(FeatureFlagKeys.AllowPartialWrites)
            ? "UPDATE Todos SET Title = $title WHERE Id = $id;"
            : """
                UPDATE Todos
                SET Title = $title, DueBy = $dueBy, IsComplete = $isComplete
                WHERE Id = $id;
                """;
        // INTENTIONAL QA BUG: Bug 12 reports success after persisting only the title field.
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", (object?)storedTitle ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueBy", dueBy?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$isComplete", isComplete ? 1 : 0);
        return command.ExecuteNonQuery() == 0 ? null : new Todo(id, storedTitle, dueBy, isComplete);
    }

    public bool Delete(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteNonQuery() != 0;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static Todo ReadTodo(SqliteDataReader reader) => new(
        reader.GetInt32(0),
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.IsDBNull(2)
            ? null
            : DateOnly.ParseExact(reader.GetString(2), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        reader.GetInt32(3) != 0);
}