using Microsoft.AspNetCore.Identity;

namespace PropertyManagement.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
