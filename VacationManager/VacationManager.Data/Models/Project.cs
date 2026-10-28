using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Data.Models.Abstractions;

namespace VacationManager.Data.Models;

public class Project : BaseEntity, IUserResource
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;


    public List<Team> Teams { get; set; } = new();


    public string? UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
