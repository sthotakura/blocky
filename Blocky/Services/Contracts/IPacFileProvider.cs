namespace Blocky.Services.Contracts;

public interface IPacFileProvider
{
    Task<string> GetAsync();
}