using Earn_Learn.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Earn_Learn.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<Korisnik> _userManager;

        public ForgotPasswordModel(UserManager<Korisnik> userManager)
        {
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Email je obavezan.")]
            [EmailAddress(ErrorMessage = "Neispravan format emaila.")]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Iz sigurnosnih razloga uvijek prikaži isti message
            // bez obzira da li korisnik postoji ili ne
            return RedirectToPage("ForgotPasswordConfirmation");
        }
    }
}