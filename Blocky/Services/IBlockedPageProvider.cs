namespace Blocky.Services;

public interface IBlockedPageProvider
{
    Task<string> GetAsync();
}