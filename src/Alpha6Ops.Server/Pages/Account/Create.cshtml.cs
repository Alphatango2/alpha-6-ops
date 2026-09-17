using System.ComponentModel.DataAnnotations;
using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Alpha6Ops.Server.Pages.Account;

public class CreateModel(AccountsService accounts, ServerSettings settings) : AccountPage(accounts, settings)
{
    [BindProperty, Required, StringLength(100)] public string Name { get; set; } = "";
    [BindProperty, Required, StringLength(50)] public string Slug { get; set; } = "";
    [BindProperty, Required, StringLength(12)] public string Callsign { get; set; } = "";
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        try { var airline = await Accounts.CreateAirlineAsync(Actor, new(Name, Slug, Callsign), ct); return RedirectToPage("Airline", new { id = airline.Id }); }
        catch (IdentityException ex) { return Failure(ex); }
    }
}
