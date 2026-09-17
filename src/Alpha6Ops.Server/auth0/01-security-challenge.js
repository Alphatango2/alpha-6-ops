// Deploy as the FIRST Auth0 Post Login v3 Action. Enable Customize MFA Factors using Actions.
// Client requests can ask for a stronger check; they can never assert that one succeeded.
exports.onExecutePostLogin = async (event, api) => {
  const policy = "http://schemas.openid.net/pape/policies/2007/06/multi-factor";
  const requested = String(event.request?.query?.acr_values || "").split(" ").includes(policy);
  if (!requested) return;

  const freshProof = (event.authentication?.methods || []).some(method =>
    (method.name === "mfa" || method.name === "passkey") &&
    Number.isFinite(Date.parse(method.timestamp)) &&
    Date.now() - Date.parse(method.timestamp) >= 0 &&
    Date.now() - Date.parse(method.timestamp) < 60_000);
  if (freshProof) return;

  // Match these choices to enabled factors in the Auth0 tenant before deploying.
  const allowed = [{ type: "otp" }, { type: "webauthn-roaming" }, { type: "webauthn-platform" }];
  const enrolled = (event.user.enrolledFactors || []).map(factor => factor.type);
  const available = allowed.filter(factor => enrolled.includes(factor.type));
  if (available.length) api.authentication.challengeWithAny(available);
  else api.authentication.enrollWithAny(allowed);
};
