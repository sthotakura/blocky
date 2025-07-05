using System.Windows;

namespace Blocky.Services.Contracts;

public interface IApplication
{
    Window? MainWindow { get; }
    
    void Shutdown();
}