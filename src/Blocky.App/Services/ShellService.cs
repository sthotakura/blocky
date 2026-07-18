using System.Diagnostics;
using Blocky.Services.Contracts;

namespace Blocky.Services;

public sealed class ShellService : IShellService
{
    public void OpenFile(string path) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
}
