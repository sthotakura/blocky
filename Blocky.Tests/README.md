# Blocky.Tests

Comprehensive test suite for the Blocky application.

## Test Framework

- **NUnit 4.x** - Primary testing framework
- **Moq 4.x** - Mocking framework for isolating dependencies
- **FluentAssertions 6.x** - Readable and expressive assertions
- **EF Core InMemory** - In-memory database for integration tests
- **Coverlet** - Code coverage analysis

## Test Structure

### Unit Tests

#### Services/BlockyServiceTests.cs
Tests for the core domain blocking logic:
- Domain matching (exact, www.* prefix, case-insensitive)
- Time-based restrictions
- Rule validation
- Edge cases and error handling

**Coverage**: ~30 tests covering all critical business logic paths

#### Services/CachedBlockyRuleRepoTests.cs
Tests for the thread-safe caching layer:
- Cache hit/miss scenarios
- Cache invalidation on CRUD operations
- Concurrent access and thread safety
- Double-checked locking pattern verification

**Coverage**: ~20 tests ensuring cache consistency and thread safety

#### ViewModels/RuleDialogViewModelTests.cs
Tests for form validation and ViewModel behavior:
- Domain validation (required, min length)
- Time restriction toggle logic
- Time range validation
- ToBlockyRule conversion

**Coverage**: ~15 tests covering all validation scenarios

### Integration Tests

#### Data/BlockyRuleRepoTests.cs
Integration tests for database persistence:
- CRUD operations with in-memory EF Core database
- TimeSpan conversion correctness
- Constraint validation
- LastUpdated timestamp accuracy

**Coverage**: ~20 tests ensuring data integrity

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter FullyQualifiedName~BlockyServiceTests

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio / Rider

Use the built-in test explorer to run and debug tests.

## Test Coverage Summary

| Component | Tests | Coverage Focus |
|-----------|-------|----------------|
| BlockyService | 30 | Core business logic |
| CachedBlockyRuleRepo | 20 | Thread safety & caching |
| BlockyRuleRepo | 20 | Data persistence |
| RuleDialogViewModel | 15 | Form validation |
| **Total** | **~85** | **Critical paths** |

## Areas for Future Testing

### High Priority
- **BlockyWebServer** - WebSocket connection handling and broadcasting
- **MainWindowViewModel** - UI workflow orchestration
- **SettingService** - Settings persistence and event handling

### Medium Priority
- **SettingsViewModel** - Settings validation
- **AppDbContext** - Additional EF Core configuration tests

### Low Priority
- **Converter classes** - Simple value converters
- **Timer services** - Utility classes

## Contributing Tests

When adding new tests:

1. **Follow the AAA pattern**: Arrange, Act, Assert
2. **Use descriptive test names**: `MethodName_Scenario_ExpectedBehavior`
3. **One assertion per test** (when possible)
4. **Use FluentAssertions** for readable assertions
5. **Mock external dependencies** to isolate the system under test
6. **Test edge cases**: null, empty, invalid inputs
7. **Test concurrent scenarios** for thread-safe code

## Example Test

```csharp
[Test]
public async Task IsDomainBlockedAsync_ExactDomainMatch_ReturnsTrue()
{
    // Arrange - Set up test data and mocks
    var domain = "example.com";
    var rule = CreateTestRule(domain);
    SetupActiveRules(rule);
    SetupCurrentTime(TimeSpan.FromHours(10));

    // Act - Execute the method under test
    var result = await _sut.IsDomainBlockedAsync(domain);

    // Assert - Verify expected behavior
    result.Should().BeTrue();
}
```

## CI/CD Integration

Tests run automatically on every push via GitHub Actions:
- `.github/workflows/build.yml` includes `dotnet test` step
- Build fails if any tests fail
- Ensures code quality before deployment
