using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("token_refresh_sessions")]
public class TokenRefreshSession : BaseModel
{
    [PrimaryKey("id")]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("stage")]
    public string? Stage { get; set; }

    [Column("progress")]
    public int Progress { get; set; } = 0;

    [Column("message")]
    public string? Message { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}