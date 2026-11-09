using Earn_Learn.Models;
using Earn_Learn.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Earn_Learn.Security
{
    public class RoleOnlyAttribute : ActionFilterAttribute
    {
        private readonly Uloga _dozvoljenaUloga;

        public RoleOnlyAttribute(Uloga uloga)
        {
            _dozvoljenaUloga = uloga;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Provjeri [AllowAnonymous] na akciji ILI na controlleru
            var allowAnonymous = context.ActionDescriptor.EndpointMetadata
                .OfType<AllowAnonymousAttribute>()
                .Any();

            if (allowAnonymous)
            {
                base.OnActionExecuting(context);
                return;
            }

            var userManager = context.HttpContext.RequestServices
                .GetRequiredService<UserManager<Korisnik>>();

            var user = userManager.GetUserAsync(context.HttpContext.User).Result;

            if (user == null || user.Uloga != _dozvoljenaUloga)
            {
                context.Result = new ViewResult
                {
                    ViewName = "PristupOdbijen",
                    StatusCode = 403
                };
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}