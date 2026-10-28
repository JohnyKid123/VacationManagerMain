using System.Linq.Expressions;
using Moq;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Core.Prototypes;
using VacationManager.Core.Services;
using VacationManager.Data.Enums;
using VacationManager.Data.Models;
using VacationManager.Data.Repositories.Abstractions;

namespace VacationManager.Test;

public class LeaveRequestServiceTests
{
    [Fact]
    public async Task ApproveAsync_SetsApprovedAndUpdatesTheRequest()
    {
        var request = PendingRequest();
        var repository = RepositoryReturning(request);
        var service = Service(repository, TestUsers.Create(Role.TeamLead));

        await service.ApproveAsync(request.Id);

        Assert.Equal(ApprovalStatus.Approved, request.ApprovalStatus);
        repository.Verify(repo => repo.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_WhenMissing_ThrowsAndDoesNotUpdate()
    {
        var repository = RepositoryReturning(null);
        var service = Service(repository, TestUsers.Create(Role.CEO));

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.ApproveAsync(Guid.NewGuid()));

        Assert.Contains("not found", error.Message, StringComparison.OrdinalIgnoreCase);
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DenyAsync_SetsDeniedAndUpdatesTheRequest()
    {
        var request = PendingRequest();
        var repository = RepositoryReturning(request);
        var service = Service(repository, TestUsers.Create(Role.CEO));

        await service.DenyAsync(request.Id);

        Assert.Equal(ApprovalStatus.Denied, request.ApprovalStatus);
        repository.Verify(repo => repo.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DenyAsync_WhenMissing_ThrowsAndDoesNotUpdate()
    {
        var repository = RepositoryReturning(null);
        var service = Service(repository, TestUsers.Create(Role.TeamLead));

        await Assert.ThrowsAsync<ArgumentException>(() => service.DenyAsync(Guid.NewGuid()));

        repository.Verify(repo => repo.UpdateAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_CopiesTheRequestAndAssignsTheSignedInUser()
    {
        var employee = TestUsers.Create(Role.Developer);
        LeaveRequest? saved = null;
        var repository = new Mock<IRepository<LeaveRequest>>();
        repository.Setup(repo => repo.CreateAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LeaveRequest, CancellationToken>((entity, _) => saved = entity)
            .Returns(Task.CompletedTask);

        var service = Service(repository, employee);
        var start = new DateTime(2026, 6, 1);
        var end = new DateTime(2026, 6, 5);
        var requested = new DateTime(2026, 5, 20);

        var created = await service.CreateAsync(new LeaveRequestPrototype
        {
            StartDate = start,
            EndDate = end,
            RequestDate = requested,
            HalfDay = true,
            Type = LeaveType.Sick,
            ApprovalStatus = ApprovalStatus.Pending,
            AttachmentPath = "notes/sick.pdf"
        }, CancellationToken.None);

        Assert.Same(saved, created);
        Assert.Equal(employee.Id, created.User?.Id);
        Assert.Equal(start, created.StartDate);
        Assert.Equal(end, created.EndDate);
        Assert.Equal(requested, created.RequestDate);
        Assert.True(created.HalfDay);
        Assert.Equal(LeaveType.Sick, created.Type);
        Assert.Equal(ApprovalStatus.Pending, created.ApprovalStatus);
        Assert.Equal("notes/sick.pdf", created.AttachmentPath);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.Equal(created.CreatedAt, created.LastModifiedAt);
    }

    [Fact]
    public async Task GetAllAsync_DeveloperSeesOnlyTheirOwnRequests()
    {
        var employee = TestUsers.Create(Role.Developer);
        var filters = await CaptureGetAllFilters(employee);

        Assert.True(Matches(filters, RequestFor(employee.Id)));
        Assert.False(Matches(filters, RequestFor(Guid.NewGuid().ToString())));
    }

    [Theory]
    [InlineData(Role.CEO)]
    [InlineData(Role.TeamLead)]
    public async Task GetAllAsync_ManagerRolesAreNotLimitedToTheirOwnRequests(Role role)
    {
        var filters = await CaptureGetAllFilters(TestUsers.Create(role));

        Assert.Empty(filters);
    }

    [Fact]
    public async Task GetByUserIdAsync_DeveloperCannotReadAnotherUsersRequests()
    {
        var employee = TestUsers.Create(Role.Developer);
        var someoneElse = Guid.NewGuid().ToString();
        var filters = await CaptureUserFilters(employee, someoneElse);

        Assert.False(Matches(filters, RequestFor(employee.Id)));
        Assert.False(Matches(filters, RequestFor(someoneElse)));
    }

    [Fact]
    public async Task GetByUserIdAsync_TeamLeadCanReadTheRequestedUsersRequests()
    {
        var lead = TestUsers.Create(Role.TeamLead);
        var employeeId = Guid.NewGuid().ToString();
        var filters = await CaptureUserFilters(lead, employeeId);

        Assert.True(Matches(filters, RequestFor(employeeId)));
        Assert.False(Matches(filters, RequestFor(lead.Id)));
    }

    [Fact]
    public async Task GetAllAsync_WithoutASignedInUser_Throws()
    {
        var auth = new Mock<IAuthenticationContext>();
        auth.SetupGet(context => context.CurrentUser).Returns((ApplicationUser?)null);
        auth.SetupGet(context => context.IsAuthenticated).Returns(false);
        var service = new LeaveRequestService(new Mock<IRepository<LeaveRequest>>().Object, auth.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAllAsync(CancellationToken.None));
    }

    private static LeaveRequest PendingRequest() => new()
    {
        Id = Guid.NewGuid(),
        StartDate = new DateTime(2026, 7, 1),
        EndDate = new DateTime(2026, 7, 3),
        ApprovalStatus = ApprovalStatus.Pending,
        Type = LeaveType.Paid
    };

    private static LeaveRequest RequestFor(string userId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ApprovalStatus = ApprovalStatus.Pending
    };

    private static Mock<IRepository<LeaveRequest>> RepositoryReturning(LeaveRequest? request)
    {
        var repository = new Mock<IRepository<LeaveRequest>>();
        repository.Setup(repo => repo.GetAsync(
                It.IsAny<IEnumerable<Expression<Func<LeaveRequest, bool>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        repository.Setup(repo => repo.UpdateAsync(It.IsAny<LeaveRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static LeaveRequestService Service(Mock<IRepository<LeaveRequest>> repository, ApplicationUser user)
        => new(repository.Object, AuthContext.For(user).Object);

    private static async Task<IReadOnlyList<Expression<Func<LeaveRequest, bool>>>> CaptureGetAllFilters(ApplicationUser user)
    {
        IReadOnlyList<Expression<Func<LeaveRequest, bool>>>? captured = null;
        var repository = new Mock<IRepository<LeaveRequest>>();
        repository.Setup(repo => repo.GetManyWithNavigationsAsync(
                It.IsAny<IEnumerable<Expression<Func<LeaveRequest, bool>>>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Expression<Func<LeaveRequest, bool>>> filters, IEnumerable<string> _, CancellationToken _) =>
            {
                captured = filters.ToArray();
                return [];
            });

        await Service(repository, user).GetAllAsync(CancellationToken.None);
        return captured ?? throw new InvalidOperationException("Filters were not captured.");
    }

    private static async Task<IReadOnlyList<Expression<Func<LeaveRequest, bool>>>> CaptureUserFilters(ApplicationUser user, string userId)
    {
        IReadOnlyList<Expression<Func<LeaveRequest, bool>>>? captured = null;
        var repository = new Mock<IRepository<LeaveRequest>>();
        repository.Setup(repo => repo.GetManyWithNavigationsAsync(
                It.IsAny<IEnumerable<Expression<Func<LeaveRequest, bool>>>>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Expression<Func<LeaveRequest, bool>>> filters, IEnumerable<string> _, CancellationToken _) =>
            {
                captured = filters.ToArray();
                return [];
            });

        await Service(repository, user).GetByUserIdAsync(userId, CancellationToken.None);
        return captured ?? throw new InvalidOperationException("Filters were not captured.");
    }

    private static bool Matches(IEnumerable<Expression<Func<LeaveRequest, bool>>> filters, LeaveRequest request)
        => filters.All(filter => filter.Compile()(request));
}
