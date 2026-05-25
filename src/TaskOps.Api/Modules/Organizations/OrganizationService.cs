using System.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TaskOps.Api.Persistence;
using TaskOps.Api.Persistence.Entities;
using TaskOps.Api.Shared.Api;
using TaskOps.Api.Shared.Security;

namespace TaskOps.Api.Modules.Organizations;

public sealed class OrganizationService(
    TaskOpsDbContext dbContext,
    ICurrentUserService currentUser,
    IOrganizationAccessService organizationAccess,
    TimeProvider timeProvider,
    IValidator<CreateOrganizationRequest> createOrganizationValidator,
    IValidator<UpdateOrganizationRequest> updateOrganizationValidator,
    IValidator<AddOrganizationMemberRequest> addMemberValidator,
    IValidator<ChangeOrganizationMemberRoleRequest> changeMemberRoleValidator) : IOrganizationService
{
    private static readonly object Empty = new();

    public async Task<ServiceResult<PagedResponse<OrganizationListItemResponse>, OrganizationFailure>> ListOrganizationsAsync(
        PageRequest page,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Failure<PagedResponse<OrganizationListItemResponse>>(OrganizationFailure.Unauthorized);
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

        return Success(
            new PagedResponse<OrganizationListItemResponse>(
                organizations.Take(limit).ToList(),
                offset,
                limit,
                organizations.Count > limit));
    }

    public async Task<ServiceResult<OrganizationResponse, OrganizationFailure>> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Failure<OrganizationResponse>(OrganizationFailure.Unauthorized);
        }

        var validation = await createOrganizationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<OrganizationResponse>(validation.ToErrorDictionary());
        }

        var normalizedSlug = OrganizationValidation.NormalizeSlug(request.Slug);
        if (await dbContext.Organizations.AnyAsync(organization => organization.Slug == normalizedSlug, cancellationToken))
        {
            return Failure<OrganizationResponse>(OrganizationFailure.DuplicateSlug);
        }

        var currentUserProfile = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (currentUserProfile is null)
        {
            return Failure<OrganizationResponse>(OrganizationFailure.NotFound);
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
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_Organizations_Slug"))
        {
            return Failure<OrganizationResponse>(OrganizationFailure.DuplicateSlug);
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

        return Success(response);
    }

    public async Task<ServiceResult<OrganizationResponse, OrganizationFailure>> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Failure<OrganizationResponse>(OrganizationFailure.Unauthorized);
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
            ? Failure<OrganizationResponse>(OrganizationFailure.NotFound)
            : Success(response);
    }

    public async Task<ServiceResult<OrganizationResponse, OrganizationFailure>> UpdateOrganizationAsync(
        Guid organizationId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return Failure<OrganizationResponse>(access);
        }

        var validation = await updateOrganizationValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<OrganizationResponse>(validation.ToErrorDictionary());
        }

        var organization = await dbContext.Organizations.FirstOrDefaultAsync(
            organization => organization.Id == organizationId,
            cancellationToken);
        if (organization is null)
        {
            return Failure<OrganizationResponse>(OrganizationFailure.NotFound);
        }

        var normalizedSlug = OrganizationValidation.NormalizeSlug(request.Slug);
        var slugTaken = await dbContext.Organizations.AnyAsync(
            organization => organization.Id != organizationId && organization.Slug == normalizedSlug,
            cancellationToken);
        if (slugTaken)
        {
            return Failure<OrganizationResponse>(OrganizationFailure.DuplicateSlug);
        }

        organization.Name = request.Name.Trim();
        organization.Slug = normalizedSlug;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_Organizations_Slug"))
        {
            return Failure<OrganizationResponse>(OrganizationFailure.DuplicateSlug);
        }

        return await GetOrganizationAsync(organizationId, cancellationToken);
    }

    public async Task<ServiceResult<PagedResponse<OrganizationMemberResponse>, OrganizationFailure>> ListMembersAsync(
        Guid organizationId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireMembershipAsync(organizationId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Failure<PagedResponse<OrganizationMemberResponse>>(ToOrganizationFailure(access.Status));
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

        return Success(
            new PagedResponse<OrganizationMemberResponse>(
                members.Take(limit).ToList(),
                offset,
                limit,
                members.Count > limit));
    }

    public async Task<ServiceResult<OrganizationMemberResponse, OrganizationFailure>> AddMemberAsync(
        Guid organizationId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return Failure<OrganizationMemberResponse>(access);
        }

        var validation = await addMemberValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<OrganizationMemberResponse>(validation.ToErrorDictionary());
        }

        _ = OrganizationValidation.TryParseRole(request.Role, out var role);
        var normalizedEmail = EmailRules.Normalize(request.Email);
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return Failure<OrganizationMemberResponse>(OrganizationFailure.UserNotFound);
        }

        var alreadyMember = await dbContext.OrganizationMembers.AnyAsync(
            member => member.OrganizationId == organizationId && member.UserId == user.Id,
            cancellationToken);
        if (alreadyMember)
        {
            return Failure<OrganizationMemberResponse>(OrganizationFailure.DuplicateMember);
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
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "IX_OrganizationMembers_OrganizationId_UserId"))
        {
            return Failure<OrganizationMemberResponse>(OrganizationFailure.DuplicateMember);
        }

        return Success(new OrganizationMemberResponse(
            member.Id,
            user.Id,
            user.Email,
            user.DisplayName,
            OrganizationMemberResponse.FormatRole(member.Role),
            member.JoinedAt));
    }

    public async Task<ServiceResult<OrganizationMemberResponse, OrganizationFailure>> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid memberId,
        ChangeOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return Failure<OrganizationMemberResponse>(access);
        }

        var validation = await changeMemberRoleValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Validation<OrganizationMemberResponse>(validation.ToErrorDictionary());
        }

        _ = OrganizationValidation.TryParseRole(request.Role, out var role);

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
                return Failure<OrganizationMemberResponse>(OrganizationFailure.NotFound);
            }

            if (member.Role == OrganizationRole.Owner &&
                role != OrganizationRole.Owner &&
                await IsLastOwnerAsync(organizationId, cancellationToken))
            {
                return Failure<OrganizationMemberResponse>(OrganizationFailure.CannotRemoveLastOwner);
            }

            member.Role = role;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Success(new OrganizationMemberResponse(
                member.Id,
                member.UserId,
                member.User.Email,
                member.User.DisplayName,
                OrganizationMemberResponse.FormatRole(member.Role),
                member.JoinedAt));
        }
        catch (Exception exception) when (PostgresErrors.IsSerializationFailure(exception))
        {
            return Failure<OrganizationMemberResponse>(OrganizationFailure.CannotRemoveLastOwner);
        }
    }

    public async Task<ServiceResult<object, OrganizationFailure>> RemoveMemberAsync(
        Guid organizationId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var access = await RequireOwnerAsync(organizationId, cancellationToken);
        if (access != OrganizationFailure.None)
        {
            return Failure<object>(access);
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
                return Failure<object>(OrganizationFailure.NotFound);
            }

            if (member.Role == OrganizationRole.Owner && await IsLastOwnerAsync(organizationId, cancellationToken))
            {
                return Failure<object>(OrganizationFailure.CannotRemoveLastOwner);
            }

            dbContext.OrganizationMembers.Remove(member);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Success<object>(Empty);
        }
        catch (Exception exception) when (PostgresErrors.IsSerializationFailure(exception))
        {
            return Failure<object>(OrganizationFailure.CannotRemoveLastOwner);
        }
    }

    private async Task<OrganizationFailure> RequireOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var access = await organizationAccess.RequireAnyRoleAsync(
            organizationId,
            OrganizationRolePolicies.OwnerOnly,
            cancellationToken);
        return access.IsAllowed ? OrganizationFailure.None : ToOrganizationFailure(access.Status);
    }

    private async Task<bool> IsLastOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var ownerCount = await dbContext.OrganizationMembers.CountAsync(
            member => member.OrganizationId == organizationId && member.Role == OrganizationRole.Owner,
            cancellationToken);

        return ownerCount <= 1;
    }

    private static OrganizationFailure ToOrganizationFailure(OrganizationAccessStatus status)
    {
        return status switch
        {
            OrganizationAccessStatus.Unauthorized => OrganizationFailure.Unauthorized,
            OrganizationAccessStatus.NotFound => OrganizationFailure.NotFound,
            OrganizationAccessStatus.Forbidden => OrganizationFailure.Forbidden,
            _ => OrganizationFailure.None
        };
    }

    private static ServiceResult<T, OrganizationFailure> Success<T>(T value) =>
        ServiceResult<T, OrganizationFailure>.Success(value, OrganizationFailure.None);

    private static ServiceResult<T, OrganizationFailure> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        ServiceResult<T, OrganizationFailure>.Validation(OrganizationFailure.Validation, errors);

    private static ServiceResult<T, OrganizationFailure> Failure<T>(OrganizationFailure failure) =>
        ServiceResult<T, OrganizationFailure>.Failed(failure);
}
