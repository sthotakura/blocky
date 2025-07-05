namespace Blocky.Services.Contracts;

public interface IBlockedPageProvider
{
    Task<string> GetAsync();
}