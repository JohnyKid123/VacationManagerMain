using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VacationManager.Data;
using VacationManager.Core.Prototypes;
using VacationManager.Core.Services;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Data.Enums;
using VacationManager.Data.Models;
using VacationManager.Data.Repositories.Abstractions;

namespace VacationManager.Test;

public class ProjectServiceTests
{
    [Fact]
    public async Task CreateAsync_AttachesTeamsOwnedByTheSignedInUser()
    {
        var owner = TestUsers.Create(Role.CEO);
        var owned = new Team { Id = Guid.NewGuid(), Name = "Platform", UserId = owner.Id };
        var repository = Repository();
        var teams = TeamServiceReturning(owned);
        var service = CreateService(repository, teams, owner);

        var created = await service.CreateAsync(Prototype("Portal", "Customer portal", owned), CancellationToken.None);

        Assert.Equal("Portal", created.Name);
        Assert.Equal("Customer portal", created.Description);
        Assert.Equal(owner.Id, created.User?.Id);
        Assert.Equal(owned.Id, Assert.Single(created.Teams).Id);
    }

    [Fact]
    public async Task CreateAsync_RejectsATeamOwnedBySomeoneElse()
    {
        var owner = TestUsers.Create(Role.CEO);
        var foreign = new Team { Id = Guid.NewGuid(), Name = "Other", UserId = Guid.NewGuid().ToString() };
        var repository = Repository();
        var service = CreateService(repository, TeamServiceReturning(foreign), owner);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(Prototype("Portal", "Customer portal", foreign), CancellationToken.None));

        repository.Verify(repo => repo.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesTheAssignedTeams()
    {
        var owner = TestUsers.Create(Role.CEO);
        var projectId = Guid.NewGuid();
        var replacement = new Team { Id = Guid.NewGuid(), Name = "Platform", UserId = owner.Id };
        var existing = new Project
        {
            Id = projectId,
            Name = "Old",
            Description = "Old description",
            Teams = { new Team { Id = Guid.NewGuid(), Name = "Legacy", UserId = owner.Id } }
        };
        var repository = Repository();
        repository.Setup(repo => repo.GetCompleteAsync(
                It.IsAny<IEnumerable<Expression<Func<Project, bool>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository.Setup(repo => repo.UpdateAsync(existing, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(repository, TeamServiceReturning(replacement), owner);

        var updated = await service.UpdateAsync(projectId, Prototype("Portal", "Customer portal", replacement), CancellationToken.None);

        Assert.Equal("Portal", updated.Name);
        Assert.Equal("Customer portal", updated.Description);
        Assert.Equal(replacement.Id, Assert.Single(updated.Teams).Id);
    }

    [Fact]
    public async Task SoftDeleteAsync_MarksTheProjectDeletedInsteadOfRemovingIt()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Portal", Description = "Customer portal" };
        var repository = Repository();
        repository.Setup(repo => repo.GetAsync(
                It.IsAny<IEnumerable<Expression<Func<Project, bool>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        repository.Setup(repo => repo.UpdateAsync(project, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(repository, new Mock<ITeamService>(), TestUsers.Create(Role.CEO));

        await service.SoftDeleteAsync(project.Id, CancellationToken.None);

        Assert.True(project.IsDeleted);
        repository.Verify(repo => repo.UpdateAsync(project, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.DeleteAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheProjectWithActiveTeamsAndSkipsDeletedProjects()
    {
        var (options, factory) = InMemoryDb.Create();
        var projectId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Projects.Add(new Project
            {
                Id = projectId,
                Name = "Portal",
                Description = "Customer portal",
                UserId = "ceo",
                Teams =
                {
                    new Team { Id = Guid.NewGuid(), Name = "Platform", UserId = "ceo" },
                    new Team { Id = Guid.NewGuid(), Name = "Archived", UserId = "ceo", IsDeleted = true }
                }
            });
            seed.Projects.Add(new Project
            {
                Id = deletedId,
                Name = "Retired",
                Description = "No longer active",
                IsDeleted = true
            });
            await seed.SaveChangesAsync();
        }

        var service = new ProjectService(
            Mock.Of<IRepository<Project>>(),
            Mock.Of<ITeamService>(),
            Mock.Of<VacationManager.Core.Authentication.Abstractions.IAuthenticationContext>(),
            factory);

        var found = await service.GetByIdAsync(projectId, CancellationToken.None);
        var missing = await service.GetByIdAsync(deletedId, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal("Portal", found.Name);
        Assert.Equal("Platform", Assert.Single(found.Teams).Name);
        Assert.Null(missing);
    }

    private static ProjectPrototype Prototype(string name, string description, params Team[] teams) => new()
    {
        Name = name,
        Description = description,
        Teams = teams.ToList()
    };

    private static Mock<IRepository<Project>> Repository()
    {
        var repository = new Mock<IRepository<Project>>();
        repository.Setup(repo => repo.CreateAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static Mock<ITeamService> TeamServiceReturning(params Team[] teams)
    {
        var service = new Mock<ITeamService>();
        foreach (var team in teams)
        {
            service.Setup(item => item.GetByIdRequiredAsync(team.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(team);
        }

        return service;
    }

    private static ProjectService CreateService(Mock<IRepository<Project>> repository, Mock<ITeamService> teams, ApplicationUser owner)
        => new(
            repository.Object,
            teams.Object,
            AuthContext.For(owner).Object,
            Mock.Of<IDbContextFactory<ApplicationDbContext>>());
}
