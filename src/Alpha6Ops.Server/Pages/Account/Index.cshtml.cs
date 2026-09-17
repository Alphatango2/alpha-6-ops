using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Alpha6Ops.Server.Pages.Account;

public class IndexModel(AccountsService accounts, ServerSettings settings) : AccountPage(accounts, settings)
{
    public BootstrapResponse? Account { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Account = await Accounts.BootstrapAsync(Actor, ct); return Page(); }
        catch (IdentityException ex) { return Failure(ex); }
    }
    public async Task<IActionResult> OnPostWorkspaceAsync(Guid? airlineId, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await OnGetAsync(ct);
        try { await Accounts.SetWorkspaceAsync(Actor, new(airlineId), ct); return RedirectToPage(); }
        catch (IdentityException ex) { return Failure(ex); }
    }
    public IActionResult OnPostLogout() => SignOut(new AuthenticationProperties { RedirectUri = "/SignedOut" }, CookieAuthenticationDefaults.AuthenticationScheme, "Auth0");
}
