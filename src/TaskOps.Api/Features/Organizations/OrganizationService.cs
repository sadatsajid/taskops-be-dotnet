using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Features.Organizations;

public sealed class OrganizationService(
    TaskOpsDbContext dbContext,
    ICurrentUserService currentUser,
    IOrganizationAccessService organizationAccess,
    TimeProvider timeProvider) : IOrganizationService
{
    private static readonly object Empty = new();
    private static readonly OrganizationRole[] OwnerOnly = [OrganizationRole.Owner];
    private static readonly string[] RoleNames = Enum.GetNames<OrganizationRole>();
    private static readonly string ValidRolesMessage = $"Role must be one of {string.Join(", ", RoleNames)}.";
    private const int MaxNameLength = 160;
    private const int MaxSlugLength = 100;
    private const int MaxEmailLength = 320;

    public async Task<OrganizationServiceResult<PagedResponse<OrganizationListItemResponse>>> ListOrganizationsAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return OrganizationServiceResult<PagedResponse<OrganizationListItemResponse>>.Failed(OrganizationFailure.Unauthorized);
        }

        var limit = page.SafeLimit;
        var offset = page.SafeOffset;
        var organizations = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId)
            .OrderBy(member => member.Organization.Name)
            .ThenBy(member => member.Organization.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(member => new OrganizationListItemResponse(
                member.Organization.Id,
                member.Organization.Name,
                member.Organization.Slug,
                OrganizationMemberResponse.FormatRole(member.Role)))
            .ToListAsync(cancellationToken);

        return OrganizationServiceResult<PagedResponse<OrganizationListItemResponse>>.Success(
            new PagedResponse<OrganizationListItemResponse>(
                organizations.Take(limit).ToList(),
                offset,
                limit,
                organizations.Count > limit));
    }

    public async Task<OrganizationServiceResult<OrganizationResponse>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.Unauthorized);
        }

        var errors = ValidateOrganization(request.Name, request.Slug);
        if (errors.Count > 0)
        {
            return OrganizationServiceResult<OrganizationResponse>.Validation(errors);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (await dbContext.Organizations.AnyAsync(organization => organization.Slug == normalizedSlug, cancellationToken))
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.DuplicateSlug);
        }

        var currentUserProfile = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (currentUserProfile is null)
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.NotFound);
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = normalizedSlug
        };

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            UserId = userId,
            Role = OrganizationRole.Owner,
            JoinedAt = timeProvider.GetUtcNow()
        };

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMembers.Add(member);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Organizations_Slug"))
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.DuplicateSlug);
        }

        var response = new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            new OrganizationMemberResponse(
                member.Id,
                userId,
                currentUserProfile.Email,
                currentUserProfile.DisplayName,
                OrganizationMemberResponse.FormatRole(member.Role),
                member.JoinedAt));

        return OrganizationServiceResult<OrganizationResponse>.Success(response);
    }

    public async Task<OrganizationServiceResult<OrganizationResponse>> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.Unauthorized);
        }

        var response = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId && member.UserId == userId)
            .Select(member => new OrganizationResponse(
                member.Organization.Id,
                member.Organization.Name,
                member.Organization.Slug,
                new OrganizationMemberResponse(
                    member.Id,
                    member.UserId,
                    member.User.Email,
                    member.User.DisplayName,
                    OrganizationMemberResponse.FormatRole(member.Role),
                    member.JoinedAt)))
            .FirstOrDefaultAsync(cancellationToken);

        return response is null
            ? OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.NotFound)
            : OrganizationServiceResult<OrganizationResponse>.Success(response);
    }

    public async Task<OrganizationServiceResult<OrganizationResponse>> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(access);
        }

        var errors = ValidateOrganization(request.Name, request.Slug);
        if (errors.Count > 0)
        {
            return OrganizationServiceResult<OrganizationResponse>.Validation(errors);
        }

        var organization = await dbContext.Organizations.FirstOrDefaultAsync(
            organization => organization.Id == organizationId,
            cancellationToken);
        if (organization is null)
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.NotFound);
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        var slugTaken = await dbContext.Organizations.AnyAsync(
            organization => organization.Id != organizationId && organization.Slug == normalizedSlug,
            cancellationToken);
        if (slugTaken)
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.DuplicateSlug);
        }

        organization.Name = request.Name.Trim();
        organization.Slug = normalizedSlug;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Organizations_Slug"))
        {
            return OrganizationServiceResult<OrganizationResponse>.Failed(OrganizationFailure.DuplicateSlug);
        }

        return await GetOrganizationAsync(organizationId, cancellationToken);
    }

    public async Task<OrganizationServiceResult<PagedResponse<OrganizationMemberResponse>>> ListMembersAsync(
        Guid organizationId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var membership = await organizationAccess.GetMembershipAsync(organizationId, cancellationToken);
        if (membership is null)
        {
            return currentUser.IsAuthenticated
                ? OrganizationServiceResult<PagedResponse<OrganizationMemberResponse>>.Failed(OrganizationFailure.NotFound)
                : OrganizationServiceResult<PagedResponse<OrganizationMemberResponse>>.Failed(OrganizationFailure.Unauthorized);
        }

        var limit = page.SafeLimit;
        var offset = page.SafeOffset;
        var members = await dbContext.OrganizationMembers
            .AsNoTracking()
            .Where(member => member.OrganizationId == organizationId)
            .OrderBy(member => member.User.DisplayName)
            .ThenBy(member => member.User.Email)
            .ThenBy(member => member.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(member => new OrganizationMemberResponse(
                member.Id,
                member.UserId,
                member.User.Email,
                member.User.DisplayName,
                OrganizationMemberResponse.FormatRole(member.Role),
                member.JoinedAt))
            .ToListAsync(cancellationToken);

        return OrganizationServiceResult<PagedResponse<OrganizationMemberResponse>>.Success(
            new PagedResponse<OrganizationMemberResponse>(
                members.Take(limit).ToList(),
                offset,
                limit,
                members.Count > limit));
    }

    public async Task<OrganizationServiceResult<OrganizationMemberResponse>> AddMemberAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Failed(access);
        }

        var errors = ValidateEmail(request.Email);
        if (!TryParseRole(request.Role, out var role))
        {
            errors["role"] = [ValidRolesMessage];
        }

        if (errors.Count > 0)
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Validation(errors);
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Failed(OrganizationFailure.UserNotFound);
        }

        var alreadyMember = await dbContext.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == organizationId && member.UserId == user.Id,
            cancellationToken);
        if (alreadyMember)
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Failed(OrganizationFailure.DuplicateMember);
        }

        var member = new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = user.Id,
            Role = role,
            JoinedAt = timeProvider.GetUtcNow()
        };

        dbContext.OrganizationMembers.Add(member);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_OrganizationMembers_OrganizationId_UserId"))
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Failed(OrganizationFailure.DuplicateMember);
        }

        return OrganizationServiceResult<OrganizationMemberResponse>.Success(new OrganizationMemberResponse(
            member.Id,
            user.Id,
            user.Email,
            user.DisplayName,
            OrganizationMemberResponse.FormatRole(member.Role),
            member.JoinedAt));
    }

    public async Task<OrganizationServiceResult<OrganizationMemberResponse>> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid memberId,
        ChangeOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Failed(access);
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Validation(new Dictionary<string, string[]>
            {
                ["role"] = [ValidRolesMessage]
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var member = await dbContext.OrganizationMembers
                .Include(member => member.User)
                .FirstOrDefaultAsync(
                    member => member.OrganizationId == organizationId && member.Id == memberId,
                    cancellationToken);
            if (member is null)
            {
                return OrganizationServiceResult<OrganizationMemberResponse>.Failed(OrganizationFailure.NotFound);
            }

            if (member.Role == OrganizationRole.Owner &&
                role != OrganizationRole.Owner &&
                await IsLastOwnerAsync(organizationId, cancellationToken))
            {
                return OrganizationServiceResult<OrganizationMemberResponse>.Failed(OrganizationFailure.CannotRemoveLastOwner);
            }

            member.Role = role;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return OrganizationServiceResult<OrganizationMemberResponse>.Success(new OrganizationMemberResponse(
                member.Id,
                member.UserId,
                member.User.Email,
                member.User.DisplayName,
                OrganizationMemberResponse.FormatRole(member.Role),
                member.JoinedAt));
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            return OrganizationServiceResult<OrganizationMemberResponse>.Failed(OrganizationFailure.CannotRemoveLastOwner);
        }
    }

    public async Task<OrganizationServiceResult<object>> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return OrganizationServiceResult<object>.Failed(access);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var member = await dbContext.OrganizationMembers.FirstOrDefaultAsync(
                member => member.OrganizationId == organizationId && member.Id == memberId,
                cancellationToken);
            if (member is null)
            {
                return OrganizationServiceResult<object>.Failed(OrganizationFailure.NotFound);
            }

            if (member.Role == OrganizationRole.Owner && await IsLastOwnerAsync(organizationId, cancellationToken))
            {
                return OrganizationServiceResult<object>.Failed(OrganizationFailure.CannotRemoveLastOwner);
            }

            dbContext.OrganizationMembers.Remove(member);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return OrganizationServiceResult<object>.Success(Empty);
        }
        catch (Exception exception) when (IsSerializationFailure(exception))
        {
            return OrganizationServiceResult<object>.Failed(OrganizationFailure.CannotRemoveLastOwner);
        }
    }

    private async Task<OrganizationFailure> RequireOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated)
        {
            return OrganizationFailure.Unauthorized;
        }

        var membership = await organizationAccess.GetMembershipAsync(organizationId, cancellationToken);
        if (membership is null)
        {
            return OrganizationFailure.NotFound;
        }

        return OwnerOnly.Contains(membership.Role) ? OrganizationFailure.None : OrganizationFailure.Forbidden;
    }

    private async Task<bool> IsLastOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var ownerCount = await dbContext.OrganizationMembers.CountAsync(
            member => member.OrganizationId == organizationId && member.Role == OrganizationRole.Owner,
            cancellationToken);

        return ownerCount <= 1;
    }

    private static Dictionary<string, string[]> ValidateOrganization(string? name, string? slug)
    {
        var errors = new Dictionary<string, string[]>();
        var trimmedName = name?.Trim() ?? string.Empty;
        var normalizedSlug = NormalizeSlug(slug ?? string.Empty);

        if (trimmedName.Length == 0 || trimmedName.Length > MaxNameLength)
        {
            errors["name"] = [$"Name must be between 1 and {MaxNameLength} characters."];
        }

        if (!IsValidSlug(normalizedSlug))
        {
            errors["slug"] = [$"Slug must be between 1 and {MaxSlugLength} characters and contain only lowercase letters, numbers, and hyphens."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateEmail(string? email)
    {
        var errors = new Dictionary<string, string[]>();
        var trimmedEmail = email?.Trim() ?? string.Empty;

        if (trimmedEmail.Length == 0 ||
            trimmedEmail.Length > MaxEmailLength ||
            !trimmedEmail.Contains('@', StringComparison.Ordinal))
        {
            errors["email"] = [$"A valid email up to {MaxEmailLength} characters is required."];
        }

        return errors;
    }

    private static bool TryParseRole(string? value, out OrganizationRole role)
    {
        role = default;
        var trimmed = value?.Trim();

        return !string.IsNullOrWhiteSpace(trimmed) &&
            RoleNames.Any(roleName => string.Equals(roleName, trimmed, StringComparison.OrdinalIgnoreCase)) &&
            Enum.TryParse(trimmed, ignoreCase: true, out role);
    }

    private static bool IsValidSlug(string slug)
    {
        if (slug.Length == 0 || slug.Length > MaxSlugLength)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(slug[0]) || !char.IsLetterOrDigit(slug[^1]))
        {
            return false;
        }

        return slug.All(character =>
            character is '-' ||
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9');
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: var actualConstraintName
        } && actualConstraintName == constraintName;

    private static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } ||
        exception is DbUpdateException
        {
            InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        };
}
