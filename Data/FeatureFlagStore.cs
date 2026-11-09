using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;

namespace APILab.Data;

public sealed record FeatureFlag(string Key, string Name, string Description, bool Enabled);

public static class FeatureFlagKeys
{
    public const string InconsistentStatusCodes = "api.inconsistent-status-codes";
    public const string OmitResponseFields = "api.omit-response-fields";
    public const string IgnoreQueryParameters = "api.ignore-query-parameters";
    public const string IncorrectPaginationMetadata = "api.incorrect-pagination-metadata";
    public const string InconsistentErrorBodies = "api.inconsistent-error-bodies";
    public const string ExposeInternalDetails = "api.expose-internal-details";
    public const string AlterDateOffsets = "api.alter-date-offsets";
    public const string SkipRequestValidation = "backend.skip-request-validation";
    public const string StaleReads = "backend.stale-reads";
    public const string DuplicateWritesOnRetry = "backend.duplicate-writes-on-retry";
    public const string LoseConcurrentUpdates = "backend.lose-concurrent-updates";
    public const string AllowPartialWrites = "backend.allow-partial-writes";
    public const string CaseSensitiveMatching = "backend.case-sensitive-matching";
    public const string TruncateStoredTitles = "backend.truncate-stored-titles";
}

public sealed class FeatureFlagStore
{
    private readonly bool canEnableFlags;

    public FeatureFlagStore(IHostEnvironment environment)
    {
        canEnableFlags = environment.IsDevelopment();
    }

    public bool CanEnableFlags => canEnableFlags;

    // INTENTIONAL QA FAULT CATALOG: These flags activate training bugs; do not remove them as cleanup.
    private static readonly FeatureFlag[] Definitions =
    [
        new(FeatureFlagKeys.InconsistentStatusCodes, "Inconsistent status codes", "Equivalent requests can receive different or contractually incorrect HTTP status codes. Compare the status for the same outcome across repeated requests and related endpoints.", false),
        new(FeatureFlagKeys.OmitResponseFields, "Omit response fields", "Successful responses may leave out properties required by the response contract. Compare the JSON fields with the documented schema and with responses from related endpoints.", false),
        new(FeatureFlagKeys.IgnoreQueryParameters, "Ignore query parameters", "Supported filters or sort parameters may be accepted but have no effect. Send requests with different parameter values and check whether the returned records or order actually changes.", false),
        new(FeatureFlagKeys.IncorrectPaginationMetadata, "Incorrect pagination metadata", "Page totals, item counts, or next-page indicators may not match the returned items. Compare the metadata against the list length and the results of requesting adjacent pages.", false),
        new(FeatureFlagKeys.InconsistentErrorBodies, "Inconsistent error bodies", "Similar failures may return different JSON structures or field names. Trigger the same kind of error through related endpoints and compare the response shape and content type.", false),
        new(FeatureFlagKeys.ExposeInternalDetails, "Expose internal details", "An error response may include implementation details such as stack traces, file paths, or database messages. Trigger a controlled failing request and inspect whether internal diagnostics are returned to the client.", false),
        new(FeatureFlagKeys.AlterDateOffsets, "Shift returned due dates", "Due dates returned by the API are shifted forward by one day without changing the stored date. Compare a record's due date in the response with the value sent when it was saved.", false),
        new(FeatureFlagKeys.SkipRequestValidation, "Skip request validation", "Blank or over-120-character titles are accepted instead of rejected. Submit each boundary case, then check the response and whether the invalid value was stored.", false),
        new(FeatureFlagKeys.StaleReads, "Return stale reads", "A read immediately after a successful create, update, or delete may return the previous state. Compare the write response with a follow-up GET and check how long it takes for the new state to appear.", false),
        new(FeatureFlagKeys.DuplicateWritesOnRetry, "Duplicate writes on retry", "Each create request writes the record twice while this flag is on, mimicking a retry without idempotency. Create one todo, then list the collection and check for duplicate entries.", false),
        new(FeatureFlagKeys.LoseConcurrentUpdates, "Lose concurrent updates", "A second update to the same todo within 500 ms is silently discarded. Send two rapid updates with different values and compare the second response with a follow-up read.", false),
        new(FeatureFlagKeys.AllowPartialWrites, "Allow partial writes", "An update returns success after saving only the title; due date and completion state remain unchanged. Update all fields, then read the todo again and compare the persisted values.", false),
        new(FeatureFlagKeys.CaseSensitiveMatching, "Case-sensitive matching", "Title filters treat values that differ only by letter case as different. Repeat the same title filter with capitalization changed and compare the matching records.", false),
        new(FeatureFlagKeys.TruncateStoredTitles, "Truncate stored titles", "Titles are cut to the first 12 characters when saved, even when the request succeeds. Create or update a longer title and compare it with the value returned by a later read.", false)
    ];

    private readonly ConcurrentDictionary<string, bool> enabledFlags = new(
        Definitions.Select(flag => new KeyValuePair<string, bool>(flag.Key, flag.Enabled)));

    public IReadOnlyList<FeatureFlag> GetAll() =>
        Definitions.Select(flag => flag with { Enabled = canEnableFlags && enabledFlags[flag.Key] }).ToArray();

    public bool IsEnabled(string key) => canEnableFlags && enabledFlags.TryGetValue(key, out var enabled) && enabled;

    public void SetEnabled(string key, bool enabled)
    {
        if (enabled && !canEnableFlags)
        {
            throw new InvalidOperationException("QA fault flags can only be enabled in the Development environment.");
        }

        if (!enabledFlags.ContainsKey(key))
        {
            throw new ArgumentException($"Unknown feature flag: {key}", nameof(key));
        }

        enabledFlags[key] = enabled;
    }
}