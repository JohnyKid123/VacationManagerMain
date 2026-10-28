using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Data.Models.Abstractions;

namespace VacationManager.Data.Models;

public class Team : BaseEntity, IUserResource
{
    public string Name { get; set; } = string.Empty;


    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }


    public string? TeamLeadId { get; set; }
    public ApplicationUser? TeamLead { get; set; }


    public List<ApplicationUser> Developers { get; set; } = new();

    public string? UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
