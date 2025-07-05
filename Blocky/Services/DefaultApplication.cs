using System.Windows;
using Blocky.Services.Contracts;

namespace Blocky.Services;

public sealed  class DefaultApplication : IApplication
{
    public Window? MainWindow => Application.Current.MainWindow;
    
    public void Shutdown()
    {
        Application.Current.Shutdown();
    }
}