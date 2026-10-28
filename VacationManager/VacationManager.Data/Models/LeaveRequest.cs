using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Data.Enums;
using VacationManager.Data.Models.Abstractions;

namespace VacationManager.Data.Models;

public class LeaveRequest : BaseEntity, IUserResource
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RequestDate { get; set; }
    public bool HalfDay { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }

    public LeaveType Type { get; set; }


    public string? AttachmentPath { get; set; } // Only for sick leave



    public string? UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
}
