using Microsoft.EntityFrameworkCore;
using Moq;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Data;
using VacationManager.Data.Enums;
using VacationManager.Data.Models;

namespace VacationManager.Test;

internal static class TestUsers
{
    public static ApplicationUser Create(Role role, string? id = null)
    {
        var userId = id ?? Guid.NewGuid().ToString();
        return new ApplicationUser
        {
            Id = userId,
            UserName = $"{role.ToString().ToLowerInvariant()}-{userId[..8]}@example.com",
            Email = $"{role.ToString().ToLowerInvariant()}-{userId[..8]}@example.com",
            FirstName = role.ToString(),
            LastName = "User",
            Role = role
        };
    }
}

internal static class AuthContext
{
    public static Mock<IAuthenticationContext> For(ApplicationUser user, params ApplicationUser[] directory)
    {
        var mock = new Mock<IAuthenticationContext>();
        mock.SetupGet(auth => auth.IsAuthenticated).Returns(true);
        mock.SetupGet(auth => auth.CurrentUser).Returns(user);
        mock.Setup(auth => auth.GetUserByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                directory.FirstOrDefault(candidate => candidate.Id == id.ToString()));
        return mock;
    }
}

internal static class InMemoryDb
{
    public static (DbContextOptions<ApplicationDbContext> Options, IDbContextFactory<ApplicationDbContext> Factory) Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(db => db.CreateDbContext()).Returns(() => new ApplicationDbContext(options));
        return (options, factory.Object);
    }
}
