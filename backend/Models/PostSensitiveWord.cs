using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Models;

[Table("PostSensitiveWord")]
public class PostSensitiveWord
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string? Word { get; set; }

    public DateTime? CreateTime { get; set; }
}
