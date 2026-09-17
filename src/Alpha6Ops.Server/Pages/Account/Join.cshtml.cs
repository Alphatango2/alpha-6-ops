using System.ComponentModel.DataAnnotations;
using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Alpha6Ops.Server.Pages.Account;

public class JoinModel(AccountsService accounts, ServerSettings settings) : AccountPage(accounts, settings)
{
    [BindProperty, Required, StringLength(256)] public string Token { get; set; } = "";
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();
        try { await Accounts.AcceptInvitationAsync(Actor, Token.Trim(), ct); return RedirectToPage("Index"); }
        catch (IdentityException ex) { ModelState.Remove(nameof(Token)); Token = ""; return Failure(ex); }
    }
}
