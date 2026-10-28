using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Moq;
using VacationManager.Core.Prototypes;
using VacationManager.Data;
using VacationManager.Core.Services;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Data.Enums;
using VacationManager.Data.Models;
using VacationManager.Data.Repositories.Abstractions;

namespace VacationManager.Test;

public class TeamServiceTests
{
    [Fact]
    public async Task CreateAsync_AssignsOwnerLeadDevelopersAndProject()
    {
        var owner = TestUsers.Create(Role.CEO);
        var lead = TestUsers.Create(Role.TeamLead);
        var developer = TestUsers.Create(Role.Developer);
        var repository = new Mock<IRepository<Team>>();
        repository.Setup(repo => repo.CreateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = CreateService(repository, AuthContext.For(owner, lead, developer));
        var projectId = Guid.NewGuid();

        var created = await service.CreateAsync(new TeamPrototype
        {
            Name = "Platform",
            Project = new Project { Name = "Portal", Description = "Customer portal" },
            ProjectId = projectId,
            TeamLeadId = lead.Id,
            Developers = [developer]
        }, CancellationToken.None);

        Assert.Equal("Platform", created.Name);
        Assert.Equal(owner.Id, created.User?.Id);
        Assert.Equal(lead.Id, created.TeamLeadId);
        Assert.Same(lead, created.TeamLead);
        Assert.Equal(projectId, created.ProjectId);
        Assert.Equal(developer.Id, Assert.Single(created.Developers).Id);
        repository.Verify(repo => repo.CreateAsync(created, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenTeamLeadDoesNotExist_ThrowsBeforeSaving()
    {
        var owner = TestUsers.Create(Role.CEO);
        var repository = new Mock<IRepository<Team>>();
        var service = CreateService(repository, AuthContext.For(owner));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new TeamPrototype
        {
            Name = "Platform",
            Project = new Project { Name = "Portal", Description = "Customer portal" },
            TeamLeadId = Guid.NewGuid().ToString()
        }, CancellationToken.None));

        Assert.Contains("Team lead", error.Message, StringComparison.OrdinalIgnoreCase);
        repository.Verify(repo => repo.CreateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenDeveloperDoesNotExist_ThrowsBeforeSaving()
    {
        var owner = TestUsers.Create(Role.CEO);
        var repository = new Mock<IRepository<Team>>();
        var service = CreateService(repository, AuthContext.For(owner));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new TeamPrototype
        {
            Name = "Platform",
            Project = new Project { Name = "Portal", Description = "Customer portal" },
            Developers = [TestUsers.Create(Role.Developer)]
        }, CancellationToken.None));

        repository.Verify(repo => repo.CreateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_UnassignsMembersAndDeletesTheTeam()
    {
        var lead = TestUsers.Create(Role.TeamLead);
        var developer = TestUsers.Create(Role.Developer);
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = "Platform",
            TeamLead = lead,
            TeamLeadId = lead.Id,
            Developers = { developer }
        };
        var repository = new Mock<IRepository<Team>>();
        repository.Setup(repo => repo.GetCompleteAsync(
                It.IsAny<IEnumerable<Expression<Func<Team, bool>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        repository.Setup(repo => repo.DeleteAsync(team, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var users = new Mock<IUserService>();
        users.Setup(service => service.ChangeRoleAsync(It.IsAny<ApplicationUser>(), Role.Unassigned, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService(repository, AuthContext.For(TestUsers.Create(Role.CEO)), users).SoftDeleteAsync(team.Id, CancellationToken.None);

        users.Verify(service => service.ChangeRoleAsync(developer, Role.Unassigned, It.IsAny<CancellationToken>()), Times.Once);
        users.Verify(service => service.ChangeRoleAsync(lead, Role.Unassigned, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(team.Developers);
        repository.Verify(repo => repo.DeleteAsync(team, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SoftDeleteAsync_WhenTeamIsMissing_Throws()
    {
        var repository = new Mock<IRepository<Team>>();
        repository.Setup(repo => repo.GetCompleteAsync(
                It.IsAny<IEnumerable<Expression<Func<Team, bool>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var service = CreateService(repository, AuthContext.For(TestUsers.Create(Role.CEO)));

        await Assert.ThrowsAsync<ArgumentException>(() => service.SoftDeleteAsync(Guid.NewGuid(), CancellationToken.None));
        repository.Verify(repo => repo.DeleteAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDevelopersForTeamAsync_ReturnsEmptyListWhenTeamIsMissing()
    {
        var repository = new Mock<IRepository<Team>>();
        repository.Setup(repo => repo.GetWithNavigationsAsync(
                It.IsAny<IEnumerable<Expression<Func<Team, bool>>>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);
        var service = CreateService(repository, AuthContext.For(TestUsers.Create(Role.CEO)));

        var developers = await service.GetDevelopersForTeamAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(developers);
    }

    [Fact]
    public async Task GetTeamByUserIdAsync_MatchesDevelopersAndTheTeamLead()
    {
        var lead = TestUsers.Create(Role.TeamLead);
        var developer = TestUsers.Create(Role.Developer);
        var stranger = TestUsers.Create(Role.Developer);
        IReadOnlyList<Expression<Func<Team, bool>>>? captured = null;
        var repository = new Mock<IRepository<Team>>();
        repository.Setup(repo => repo.GetWithNavigationsAsync(
                It.IsAny<IEnumerable<Expression<Func<Team, bool>>>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Expression<Func<Team, bool>>> filters, IEnumerable<string> _, CancellationToken _) =>
            {
                captured = filters.ToArray();
                return null;
            });
        var service = CreateService(repository, AuthContext.For(TestUsers.Create(Role.CEO)));

        await service.GetTeamByUserIdAsync(developer.Id, CancellationToken.None);
        Assert.NotNull(captured);
        Assert.True(Matches(captured, new Team { Developers = { developer } }));
        Assert.False(Matches(captured, new Team { Developers = { stranger }, TeamLeadId = lead.Id }));

        await service.GetTeamByUserIdAsync(lead.Id, CancellationToken.None);
        Assert.NotNull(captured);
        Assert.True(Matches(captured, new Team { TeamLeadId = lead.Id }));
        Assert.False(Matches(captured, new Team { Developers = { stranger } }));
    }

    [Fact]
    public async Task GetByIdAsync_LoadsDevelopersThroughTheTeamServiceContract()
    {
        var (options, factory) = InMemoryDb.Create();
        var developer = TestUsers.Create(Role.Developer);
        var teamId = Guid.NewGuid();

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Users.Add(developer);
            seed.Teams.Add(new Team
            {
                Id = teamId,
                Name = "Platform",
                Developers = { developer }
            });
            await seed.SaveChangesAsync();
        }

        ITeamService service = CreateService(new Mock<IRepository<Team>>(), AuthContext.For(TestUsers.Create(Role.CEO)), dbContextFactory: factory);

        var team = await service.GetByIdAsync(teamId, CancellationToken.None);

        Assert.NotNull(team);
        Assert.Equal("Platform", team.Name);
        Assert.Equal(developer.FirstName, Assert.Single(team.Developers).FirstName);
    }

    private static bool Matches(IEnumerable<Expression<Func<Team, bool>>> filters, Team team)
        => filters.All(filter => filter.Compile()(team));

    private static TeamService CreateService(
        Mock<IRepository<Team>> repository,
        Mock<VacationManager.Core.Authentication.Abstractions.IAuthenticationContext> auth,
        Mock<IUserService>? users = null,
        IDbContextFactory<ApplicationDbContext>? dbContextFactory = null)
    {
        return new TeamService(
            repository.Object,
            auth.Object,
            (users ?? new Mock<IUserService>()).Object,
            dbContextFactory ?? Mock.Of<IDbContextFactory<ApplicationDbContext>>());
    }
}
