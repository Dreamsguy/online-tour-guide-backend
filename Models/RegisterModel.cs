using System.ComponentModel.DataAnnotations;

public class RegisterModel
{
    [Required]
    public string Email { get; set; }
    public string Name { get; set; }

    [Required]
    public string Password { get; set; }

    [Required]
    public string Role { get; set; } // "Guide", "Manager", "Admin", "User"

    // OrganizationId обязателен для Guide и Manager
    [RequiredIfRole("Guide", "Manager", ErrorMessage = "OrganizationId обязателен для роли Guide или Manager")]
    public int? OrganizationId { get; set; }
}

// Кастомный атрибут валидации
public class RequiredIfRoleAttribute : ValidationAttribute
{
    private readonly string[] _roles;

    public RequiredIfRoleAttribute(params string[] roles)
    {
        _roles = roles;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var model = (RegisterModel)validationContext.ObjectInstance;
        if (_roles.Contains(model.Role) && (value == null || (int?)value == 0))
        {
            return new ValidationResult(ErrorMessage);
        }
        return ValidationResult.Success;
    }
}