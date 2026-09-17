// Deploy as the SECOND Auth0 Post Login v3 Action, after the completed security challenge.
// Do not move this into the challenge Action: methods are updated in the subsequent Action.
exports.onExecutePostLogin = async (event, api) => {
  const prefix = "https://alpha6ops.com/claims/";
  const proofs = (event.authentication?.methods || [])
    .filter(method => method.name === "mfa" || method.name === "passkey")
    .map(method => Date.parse(method.timestamp))
    .filter(time => Number.isFinite(time) && time > 0 && time <= Date.now());
  const proofTime = proofs.length ? Math.max(...proofs) : null;
  const claims = {
    email: event.user.email || "",
    email_verified: event.user.email_verified === true,
    display_name: event.user.name || event.user.nickname || event.user.email || "Pilot",
    mfa: proofTime !== null
  };
  if (proofTime !== null) claims.mfa_at = Math.floor(proofTime / 1000);
  for (const [name, value] of Object.entries(claims)) {
    api.idToken.setCustomClaim(prefix + name, value);
    api.accessToken.setCustomClaim(prefix + name, value);
  }
};
