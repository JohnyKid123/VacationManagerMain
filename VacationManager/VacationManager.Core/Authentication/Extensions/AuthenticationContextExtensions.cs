using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VacationManager.Core.Authentication.Abstractions;
using VacationManager.Data.Models;

namespace VacationManager.Core.Authentication.Extensions;

public static class AuthenticationContextExtensions
{
    public static ApplicationUser GetCurrentUserRequired(this IAuthenticationContext authContext)
    {
        if (authContext.CurrentUser is null)
        {
            throw new InvalidOperationException("This action requires an authenticated user.");
        }

        return authContext.CurrentUser;
    }
}
