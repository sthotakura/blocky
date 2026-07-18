# Blocky.Tests

Unit and integration tests for the Blocky application.

## Stack

- **NUnit 4.x** — test framework
- **Moq 4.x** — mocking
- **FluentAssertions 8.x** — assertions
- **EF Core InMemory** — in-memory database for repo tests
- **Coverlet** — code coverage

---

## Test files

| File | What it tests |
|------|---------------|
| `Services/BlockyServiceTests.cs` | Core domain blocking logic, time windows, rule CRUD events |

---

## Running tests

```bash
# All tests
dotnet test Blocky.sln

# With output
dotnet test Blocky.sln --verbosity normal

# Specific class
dotnet test --filter FullyQualifiedName~BlockyServiceTests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Coverage areas (BlockyServiceTests — 52 tests)

| Area | What's covered |
|------|----------------|
| `AddRuleAsync` | Calls repo, fires `RulesChanged`, rejects null |
| `UpdateRuleAsync` | Calls repo, fires `RulesChanged`, rejects null |
| `RemoveRuleAsync` | Calls repo, fires `RulesChanged`, rejects empty Guid |
| `GetRuleAsync` | Returns from repo, rejects empty Guid |
| `GetAllRulesAsync` | Delegates to repo |
| `IsDomainBlockedAsync` | Exact match, subdomain match, multi-level subdomain, case-insensitive, time windows (in/out/boundary), null/empty input |
| `GetBlockedDomainsAsync` | Active rules, time windows, deduplication (case-insensitive) |

---

## Conventions

- Pattern: `MethodName_Scenario_ExpectedBehavior`
- Arrange / Act / Assert structure
- `IDateTimeService` mocked to control time-based tests
- `ICachedBlockyRuleRepo` mocked to isolate service logic

---

## Example

```csharp
[Test]
public async Task IsDomainBlockedAsync_WithTimeRestriction_WithinTimeWindow_ReturnsTrue()
{
    var rule = CreateTestRuleWithTime("example.com",
        startTime: TimeSpan.FromHours(8),
        endTime: TimeSpan.FromHours(17));
    SetupActiveRules(rule);
    SetupCurrentTime(TimeSpan.FromHours(10));

    var result = await _sut.IsDomainBlockedAsync("example.com");

    result.Should().BeTrue();
}
```

---

## CI

Tests run on every version tag push via `.github/workflows/build.yml`. Build fails if any test fails.
