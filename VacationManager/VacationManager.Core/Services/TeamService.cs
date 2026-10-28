using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Core.Authentication.Extensions;
using VacationManager.Core.Prototypes;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Data;
using VacationManager.Data.Enums;
using VacationManager.Data.Models;
using VacationManager.Data.Repositories;
using VacationManager.Data.Repositories.Abstractions;

namespace VacationManager.Core.Services
{
    public class TeamService(IRepository<Team> repository, IAuthenticationContext authContext, IUserService userService, IDbContextFactory<ApplicationDbContext> dbContextFactory) : BaseService<Team, TeamPrototype>(repository), ITeamService
    {
        private readonly IAuthenticationContext _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));
        private readonly IUserService _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        protected override Task<Team> InitializeAsync(TeamPrototype prototype, CancellationToken cancellationToken)
         => Task.FromResult(new Team());


        protected override async Task ApplyAsync(Team entity, TeamPrototype prototype, CancellationToken cancellationToken)
        {

            entity.Name = prototype.Name;

            entity.User = this._authContext.GetCurrentUserRequired();

            if (!string.IsNullOrWhiteSpace(prototype.TeamLeadId))
            {
                var teamLeadGuid = Guid.Parse(prototype.TeamLeadId);
                var teamLeadUser = await this._authContext.GetUserByIdAsync(teamLeadGuid, cancellationToken);

                if (teamLeadUser == null)
                {
                    throw new ArgumentException($"Team lead with id {prototype.TeamLeadId} not found");
                }

                entity.TeamLeadId = prototype.TeamLeadId;
                entity.TeamLead = teamLeadUser;
            }

            
            if (prototype.Developers != null)
            {
                entity.Developers = new List<ApplicationUser>();
                foreach (var developer in prototype.Developers)
                {
                    var userId = Guid.Parse(developer.Id);
                    var user = await this._authContext.GetUserByIdAsync(userId, cancellationToken);

                    if (user == null)
                    {
                        throw new ArgumentException($"User with id {developer.Id} not found");
                    }
                    entity.Developers.Add(user);
                }
            }
            else
            {
                entity.Developers = new List<ApplicationUser>();
            }

            entity.ProjectId = prototype.ProjectId;

        }
        public override async Task<Team[]> GetAllAsync(CancellationToken cancellationToken)
        {
            var includes = new[]
            {
                nameof(Team.Developers),
                nameof(Team.TeamLead),
                nameof(Team.Project),
                nameof(Team.User)
            };

            return await Repository.GetManyWithNavigationsAsync([], includes, cancellationToken);
        }
        protected override IEnumerable<Expression<Func<Team, bool>>> BuildAdditionalFilters()
        {
            ApplicationUser currentUser = this._authContext.GetCurrentUserRequired();
            return [team => team.UserId == currentUser.Id];
        }
        public override async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            using var context = _dbContextFactory.CreateDbContext();

            return await context.Teams
                .Include(t => t.Project)
                .Include(t => t.TeamLead)
                .Include(t => t.Developers)
                .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }
        public override async Task SoftDeleteAsync(Guid teamId, CancellationToken cancellationToken)
        {
            var team = await Repository.GetCompleteAsync([t => t.Id == teamId], cancellationToken);
            if (team == null) throw new ArgumentException("Team not found");

            foreach (var developer in team.Developers)
            {
                await _userService.ChangeRoleAsync(developer, Role.Unassigned, cancellationToken);
            }

            if (team.TeamLead != null)
            {
                await _userService.ChangeRoleAsync(team.TeamLead, Role.Unassigned, cancellationToken);
            }

            team.Developers.Clear();

            await Repository.DeleteAsync(team, cancellationToken);
        }
        public async Task<Team?> GetTeamByUserIdAsync(string userId, CancellationToken cancellationToken)
        {
            var includes = new[]
            {
                nameof(Team.Developers),
                nameof(Team.TeamLead)
            };

            var filters = new Expression<Func<Team, bool>>[]
            {
                team => team.Developers.Any(developer => developer.Id == userId) || team.TeamLeadId == userId
            };

            return await Repository.GetWithNavigationsAsync(filters, includes, cancellationToken);
        }
        public async Task<List<ApplicationUser>> GetDevelopersForTeamAsync(Guid teamId, CancellationToken cancellationToken)
        {
            var includes = new[] { nameof(Team.Developers) };
            var filters = new Expression<Func<Team, bool>>[] { team => team.Id == teamId };

            var team = await Repository.GetWithNavigationsAsync(filters, includes, cancellationToken);
            return team?.Developers ?? new List<ApplicationUser>();
        }


    }
}




