using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Core.Authentication.Extensions;
using VacationManager.Core.Prototypes;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Data;
using VacationManager.Data.Models;
using VacationManager.Data.Models.Abstractions;
using VacationManager.Data.Repositories;
using VacationManager.Data.Repositories.Abstractions;

namespace VacationManager.Core.Services
{
    public class ProjectService(
        IRepository<Project> repository,
        ITeamService teamService,
        IAuthenticationContext authContext,
        IDbContextFactory<ApplicationDbContext> dbContextFactory
    ) : BaseService<Project, ProjectPrototype>(repository), IProjectService
    {
        private readonly ITeamService _teamService = teamService ?? throw new ArgumentNullException(nameof(teamService));
        private readonly IAuthenticationContext _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));


        protected override async Task ApplyAsync(Project entity, ProjectPrototype prototype, CancellationToken cancellationToken)
        {
            var currentUser = _authContext.GetCurrentUserRequired();
            entity.Name = prototype.Name;
            entity.Description = prototype.Description;
            entity.User = currentUser;

            entity.Teams.Clear();

            foreach (var team in prototype.Teams)
            {
                var existingTeam = await _teamService.GetByIdRequiredAsync(team.Id, cancellationToken);

                if (!string.Equals(existingTeam.UserId, currentUser.Id, StringComparison.Ordinal))
                {
                    throw new UnauthorizedAccessException($"User does not own the team with id {team.Id}");
                }

                entity.Teams.Add(existingTeam);
            }
        }


        public override async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            using var context = _dbContextFactory.CreateDbContext();

            return await context.Projects
                .Include(p => p.Teams)
                    .ThenInclude(t => t.Developers)
                .Include(p => p.Teams)
                    .ThenInclude(t => t.TeamLead)
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, cancellationToken);
        }



        protected override IEnumerable<Expression<Func<Project, bool>>> BuildAdditionalFilters()
        {
            if (!_authContext.IsAuthenticated)
            {
                return Enumerable.Empty<Expression<Func<Project, bool>>>();
            }

            var currentUser = _authContext.GetCurrentUserRequired();
            return [project => project.UserId == currentUser.Id];
        }
    }
}
