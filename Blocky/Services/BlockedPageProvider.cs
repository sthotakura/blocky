using System.IO;
using Blocky.Services.Contracts;

namespace Blocky.Services;

public sealed class BlockedPageProvider : IBlockedPageProvider
{
    readonly Task<string> _initializeTask = InitializeAsync();

    static Task<string> InitializeAsync() => File.ReadAllTextAsync(Path.Combine("Resources", "blocked.html"));

    public Task<string> GetAsync() => _initializeTask;
}