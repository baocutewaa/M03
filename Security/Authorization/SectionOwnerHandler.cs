using M03_Opus.Domain.Entities;
using M03_Opus.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace M03_Opus.Security.Authorization;

public sealed class SectionOwnerRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Kiểm soát phạm vi lớp học phần cho Giảng viên (chỉ lớp mình dạy) và Giáo vụ theo khoa.
/// </summary>
public sealed class SectionOwnerHandler : AuthorizationHandler<SectionOwnerRequirement, CourseSectionViewRow>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SectionOwnerRequirement requirement,
        CourseSectionViewRow resource)
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
                var allowed = scopeClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (allowed.Any(id => int.TryParse(id, out var fid) && fid == resource.CourseFacultyId))
                {
                    context.Succeed(requirement);
                }
            }
            return Task.CompletedTask;
        }

        if (user.IsInRole(AppRoles.Lecturer))
        {
            if (int.TryParse(user.FindFirst(SmsClaimTypes.LecturerId)?.Value, out var lid) && lid == resource.LecturerId)
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
