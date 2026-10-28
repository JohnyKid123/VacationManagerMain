using System.Linq.Expressions;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Core.Authentication.Extensions;
using VacationManager.Core.Prototypes;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Data.Enums;
using VacationManager.Data.Models;
using VacationManager.Data.Repositories.Abstractions;


namespace VacationManager.Core.Services
{
    public class LeaveRequestService(
        IRepository<LeaveRequest> repository,
        IAuthenticationContext authContext
    ) : BaseService<LeaveRequest, LeaveRequestPrototype>(repository), ILeaveRequestService
    {
        private readonly IAuthenticationContext _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));

        public async Task ApproveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var filters = new List<Expression<Func<LeaveRequest, bool>>>
    {
        r => r.Id == id
    };

            var request = await Repository.GetAsync(filters, cancellationToken);
            if (request == null)
                throw new ArgumentException("Leave request not found");

            request.ApprovalStatus = ApprovalStatus.Approved;

            await Repository.UpdateAsync(request, cancellationToken);
        }

        public async Task DenyAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var filters = new List<Expression<Func<LeaveRequest, bool>>>
    {
        r => r.Id == id
    };

            var request = await Repository.GetAsync(filters, cancellationToken);
            if (request == null)
                throw new ArgumentException("Leave request not found");

            request.ApprovalStatus = ApprovalStatus.Denied;

            await Repository.UpdateAsync(request, cancellationToken);
        }



        protected override Task ApplyAsync(LeaveRequest entity, LeaveRequestPrototype prototype, CancellationToken cancellationToken)
        {
            entity.User = _authContext.GetCurrentUserRequired();
            entity.StartDate = prototype.StartDate;
            entity.EndDate = prototype.EndDate;
            entity.RequestDate = prototype.RequestDate;
            entity.HalfDay = prototype.HalfDay;
            entity.Type = prototype.Type;
            entity.AttachmentPath = prototype.AttachmentPath;
            entity.ApprovalStatus = prototype.ApprovalStatus;
            return Task.CompletedTask;
        }

        public override async Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var filters = BuildAdditionalFilters().ToList();

            filters.Add(lr => lr.Id == id);

            return await Repository.GetWithNavigationsAsync(
                filters,
                new[] { "User" },
                cancellationToken
            );
        }

        public async Task<List<LeaveRequest>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            var filters = BuildAdditionalFilters().ToList();
            filters.Add(leaveRequest => leaveRequest.UserId == userId);

            var leaveRequests = await Repository.GetManyWithNavigationsAsync(
                filters,
                new[] { "User" },
                cancellationToken
            );

            return leaveRequests.ToList();
        }

        public override async Task<LeaveRequest[]> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var filters = BuildAdditionalFilters();
            return await Repository.GetManyWithNavigationsAsync(
                filters,
                new[] { "User" },
                cancellationToken
            );
        }

        protected override IEnumerable<Expression<Func<LeaveRequest, bool>>> BuildAdditionalFilters()
        {
            var currentUser = _authContext.GetCurrentUserRequired();

            if (currentUser.Role is Role.CEO or Role.TeamLead)
            {
                return [];
            }

            return [leaveRequest => leaveRequest.UserId == currentUser.Id];
        }

    }
}
