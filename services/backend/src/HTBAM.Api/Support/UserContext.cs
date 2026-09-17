using System.Security.Claims;

namespace HTBAM.Api.Support;

public static class UserContext
{
    public static long? Id(ClaimsPrincipal user) => long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
