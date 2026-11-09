# Intentional QA Bugs

These behaviors are deliberate test scenarios, not accidental defects. Do not correct or refactor them without retiring the matching Bug flag and QA exercise. Each fault is disabled by default and can only be enabled in the Development environment.

| Bug | Flag key | Deliberate behavior |
| --- | --- | --- |
| Bug 1 | `api.inconsistent-status-codes` | Creates return 200 instead of 201; missing-resource requests return 200 instead of 404. |
| Bug 2 | `api.omit-response-fields` | REST JSON and SOAP responses omit the `dueBy` field. |
| Bug 3 | `api.ignore-query-parameters` | The list endpoint accepts title, completion, and sort parameters but ignores them. |
| Bug 4 | `api.incorrect-pagination-metadata` | Paginated responses report total item and page counts one higher than reality. |
| Bug 5 | `api.inconsistent-error-bodies` | Similar not-found responses use different JSON shapes depending on the operation. |
| Bug 6 | `api.expose-internal-details` | A negative ID returns a 500 with a fixed synthetic diagnostic; no real stack trace or secret is exposed. |
| Bug 7 | `api.alter-date-offsets` | API responses shift due dates forward by one day without changing stored data. |
| Bug 8 | `backend.skip-request-validation` | Blank and over-120-character titles are accepted by REST and SOAP operations. |
| Bug 9 | `backend.stale-reads` | The first list read is cached until the flag is disabled, hiding later writes. |
| Bug 10 | `backend.duplicate-writes-on-retry` | Each create request inserts the record twice. |
| Bug 11 | `backend.lose-concurrent-updates` | A second update to the same item within 500 ms is discarded while returning the current record. |
| Bug 12 | `backend.allow-partial-writes` | Updates persist only the title while returning the requested values as if all fields were saved. |
| Bug 13 | `backend.case-sensitive-matching` | Title filters become case-sensitive. |
| Bug 14 | `backend.truncate-stored-titles` | Titles are truncated to 12 characters when created or updated. |

The `INTENTIONAL QA BUG` comments beside the implementation branches point back to these scenarios. Keep the flag ordering in `Data/FeatureFlagStore.cs` aligned with the Bug numbers shown in the feature-switch page.