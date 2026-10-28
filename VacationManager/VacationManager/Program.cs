using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VacationManager.Components;
using VacationManager.Components.Account;
using VacationManager.Data;
using VacationManager.Data.Models;
using VacationManager.Data.Enums;
using VacationManager.Data.Repositories;
using VacationManager.Data.Repositories.Abstractions;
using VacationManager.Profiles;
using Microsoft.AspNetCore.Cors.Infrastructure;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Core.Authentication;
using VacationManager.Core.Services.Abstractions;
using VacationManager.Core.Services;
using VacationManager.Middleware;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing. Set it in appsettings.Development.json, with user-secrets, or with the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

Assembly currentAssembly = Assembly.GetExecutingAssembly();
builder.Services.AddAutoMapper(currentAssembly);
builder.Services.AddAutoMapper(typeof(TeamProfile));

builder.Services.AddScoped<IAuthenticationContext, AuthenticationContext>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped(typeof(IIdentityRepository<>), typeof(IdentityRepository<>));
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireCEORole", policy =>
        policy.Requirements.Add(new HasAnyRoleRequirement(Role.CEO)));
    options.AddPolicy("RequireCEOOrTLRole", policy =>
        policy.Requirements.Add(new HasAnyRoleRequirement(Role.TeamLead, Role.CEO)));
});

builder.Services.AddScoped<IAuthorizationHandler, HasAnyRoleHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<AuthenticationContextSetupMiddleware>();

app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
