using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DbMigrator.Entities;

[Table("balance_events_log")]
public class BalanceEventLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("amount", TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column("balance_before", TypeName = "decimal(18,2)")]
    public decimal BalanceBefore { get; set; }

    [Column("balance_after", TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    [Column("correlation_id")]
    public Guid CorrelationId { get; set; }

    [Column("received_at")]
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
