using Microsoft.AspNetCore.Authorization;

namespace M03_Opus.Security.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
