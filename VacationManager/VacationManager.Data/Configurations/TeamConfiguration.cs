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

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ConfigureEntity();

        builder.HasOne(t => t.Project)
               .WithMany(p => p.Teams)
               .HasForeignKey(t => t.ProjectId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.User)
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Developers)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                "TeamDevelopers",
                r => r.HasOne<ApplicationUser>().WithMany().HasForeignKey("DeveloperId"),
                l => l.HasOne<Team>().WithMany().HasForeignKey("TeamId")
                .OnDelete(DeleteBehavior.Restrict))
                .ToTable("TeamDevelopers");

        builder.HasOne(t => t.TeamLead)
               .WithMany()
               .HasForeignKey(t => t.TeamLeadId)
               .OnDelete(DeleteBehavior.Restrict);



    }
}
