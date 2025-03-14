using System.Windows;

namespace Blocky.Services;

public sealed  class DefaultApplication : IApplication
{
    public Window? MainWindow => Application.Current.MainWindow;
}