namespace Blocky.Services.Contracts;

/// <summary>OS shell interactions (opening files with their default app).</summary>
public interface IShellService
{
    void OpenFile(string path);
}
