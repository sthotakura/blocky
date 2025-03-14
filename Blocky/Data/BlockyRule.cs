using System.ComponentModel.DataAnnotations;

namespace Blocky.Data;

public class BlockyRule
{
    [Key]
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string Domain { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    
    public bool HasTimeRestriction { get; set; }
    
    public TimeSpan? StartTime { get; set; }
    
    public TimeSpan? EndTime { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUpdated { get; set; } = DateTime.UtcNow;

    public override string ToString() => $"Id: {Id}, Domain: {Domain}, IsEnabled: {IsEnabled}, HasTimeRestriction: {HasTimeRestriction}, StartTime: {StartTime}, EndTime: {EndTime}, CreatedAt: {CreatedAt}, LastUpdated: {LastUpdated}";
}