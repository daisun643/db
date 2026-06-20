namespace Backend.Models.DTOs;

public class AddCreditRequest
{
    public int UserId { get; set; }
    public int Credit { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class UserLevelResponse
{
    public int UserId { get; set; }
    public int CurrentLevel { get; set; }
    public int TotalCredit { get; set; }
    public int NextLevelRequirement { get; set; }
    public int CreditToNextLevel { get; set; }
}

public class UserCreditResponse
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public int UserLevel { get; set; }
    public int TotalCredit { get; set; }
    public int Credit { get; set; }
}

public class CreditAdjustmentResponse
{
    public int CreditAdjustmentId { get; set; }
    public int? UserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int ChangePoints { get; set; }
    public DateTime? AdjustTime { get; set; }
}

public class UpdateProfileRequest
{
    public string Username { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
