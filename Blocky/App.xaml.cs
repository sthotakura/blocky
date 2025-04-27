using System.Windows;

namespace Blocky;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    readonly Bootstrapper _bootstrapper = new();
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        _bootstrapper.Run();
    }
}