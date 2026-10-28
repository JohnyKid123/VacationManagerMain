using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Data.Models;
using VacationManager.Data.Configurations.Extensions;

namespace VacationManager.Data.Configurations;

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ConfigureEntity();

        builder.HasOne(l => l.User)
               .WithMany(u => u.LeaveRequests)
               .HasForeignKey(l => l.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
