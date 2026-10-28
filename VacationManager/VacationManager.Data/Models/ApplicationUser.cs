using Microsoft.AspNetCore.Identity;
using VacationManager.Data.Enums;
using VacationManager.Data.Models.Abstractions;

namespace VacationManager.Data.Models;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser, IIdentityEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public List<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();

    public Role Role { get; set; } = Role.Unassigned;
}

