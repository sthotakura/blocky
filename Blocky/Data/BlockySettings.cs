using System.ComponentModel.DataAnnotations;

namespace Blocky.Data;

public class BlockySettings
{
    [Key] public Guid Id { get; set; } = Guid.Parse("3E6FF4BA-90D4-45B6-A628-65CD5F310AC7");

    public int ProxyPort { get; set; } = 8080;

    public bool IsRunning { get; set; } = false;
    
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}