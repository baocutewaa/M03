using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace M03_Opus.Security.Authorization;

public sealed class StudentScopeRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Kiểm soát truy cập hồ sơ sinh viên tầng ứng dụng (Tình huống 18 - song song với RLS ở SQL).
/// </summary>
public sealed class StudentScopeHandler : AuthorizationHandler<StudentScopeRequirement, StudentDetailViewRow>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StudentScopeRequirement requirement,
        StudentDetailViewRow resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(resource);

        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        if (user.IsInRole(AppRoles.Administrator))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (user.IsInRole(AppRoles.Staff))
        {
            var scopeClaim = user.FindFirst(SmsClaimTypes.FacultyScope)?.Value;
            if (!string.IsNullOrWhiteSpace(scopeClaim))
            {
                var allowedFaculties = scopeClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowedFaculties.Any(id => int.TryParse(id, out var fid) && fid == resource.FacultyId))
                {
                    context.Succeed(requirement);
                }
            }
            return Task.CompletedTask;
        }

        if (user.IsInRole(AppRoles.Student))
        {
            if (int.TryParse(user.FindFirst(SmsClaimTypes.StudentId)?.Value, out var sid) && sid == resource.StudentId)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
