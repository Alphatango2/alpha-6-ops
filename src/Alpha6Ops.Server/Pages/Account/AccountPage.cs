using Alpha6Ops.Accounts;
using Alpha6Ops.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alpha6Ops.Server.Pages.Account;

public abstract class AccountPage(AccountsService accounts, ServerSettings settings) : PageModel
{
    protected AccountsService Accounts { get; } = accounts;
    protected ActorIdentity Actor => TrustedActor.Read(User, settings);
    public string? ErrorMessage { get; protected set; }
    public bool NeedsVerification { get; protected set; }
    protected IActionResult Failure(IdentityException error)
    {
        ErrorMessage = error.Message;
        NeedsVerification = error.Code is "mfa_required" or "recent_authentication_required" or "recent_mfa_required";
        Response.StatusCode = error.StatusCode;
        return Page();
    }
}
