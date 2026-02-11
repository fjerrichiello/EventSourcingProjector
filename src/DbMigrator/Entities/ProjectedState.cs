using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DbMigrator.Entities;

[Table("projected_state")]
public class ProjectedState
{
    [Key]
    [MaxLength(50)]
    [Column("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [Column("current_balance", TypeName = "decimal(18,2)")]
    public decimal CurrentBalance { get; set; }

    [Column("last_processed_sequence")]
    public long LastProcessedSequence { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
