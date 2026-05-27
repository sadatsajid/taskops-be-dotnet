using System.Reflection;
using FluentAssertions;
using TaskOps.Application.Modules.Organizations.Access;
using TaskOps.Application.SharedKernel.Security;
using TaskOps.Domain.Modules.Issues;
using TaskOps.Infrastructure.Persistence;

namespace TaskOps.Api.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly Assembly[] ProductionAssemblies =
    [
        typeof(Program).Assembly,
        typeof(IOrganizationAccessService).Assembly,
        typeof(Issue).Assembly,
        typeof(TaskOpsDbContext).Assembly
    ];

    [Fact]
    public void ProductionTypes_DoNotUsePreviousLayerFirstNamespaces()
    {
        var staleNamespaces = ProductionAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => type.Namespace)
            .Where(namespaceName => namespaceName is not null)
            .Cast<string>()
            .Where(namespaceName =>
                namespaceName.Contains(".Features.", StringComparison.Ordinal) ||
                namespaceName.StartsWith("TaskOps.Application.Shared.", StringComparison.Ordinal) ||
                namespaceName.StartsWith("TaskOps.Domain.Entities", StringComparison.Ordinal) ||
                namespaceName.StartsWith("TaskOps.Domain.Security", StringComparison.Ordinal))
            .Distinct()
            .Order()
            .ToArray();

        staleNamespaces.Should().BeEmpty();
    }

    [Fact]
    public void DomainTypes_LiveInModulesOrSharedKernel()
    {
        var misplacedTypes = typeof(Issue).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null)
            .Where(type =>
                !type.Namespace!.StartsWith("TaskOps.Domain.Modules.", StringComparison.Ordinal) &&
                !type.Namespace.StartsWith("TaskOps.Domain.SharedKernel", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .Order()
            .ToArray();

        misplacedTypes.Should().BeEmpty();
    }

    [Fact]
    public void OrganizationAccess_IsAnOrganizationsModuleContract()
    {
        typeof(IOrganizationAccessService).Namespace.Should().Be("TaskOps.Application.Modules.Organizations.Access");
        typeof(IOrganizationContext).Namespace.Should().Be("TaskOps.Application.Modules.Organizations.Access");
        typeof(ICurrentUserService).Namespace.Should().Be("TaskOps.Application.SharedKernel.Security");
    }
}
