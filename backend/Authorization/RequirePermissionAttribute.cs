using Microsoft.AspNetCore.Authorization;

namespace Backend.Authorization;

public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(params string[] permissions)
    {
        Policy = string.Join(",", permissions);
    }
}
