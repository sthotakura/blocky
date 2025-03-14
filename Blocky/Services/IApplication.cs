using System.Windows;

namespace Blocky.Services;

public interface IApplication
{
    Window? MainWindow { get; }
}