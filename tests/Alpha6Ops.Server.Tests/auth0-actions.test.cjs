const assert = require('node:assert/strict');
const security = require('../../src/Alpha6Ops.Server/auth0/01-security-challenge.js');
const claimsAction = require('../../src/Alpha6Ops.Server/auth0/02-account-claims.js');
const prefix = 'https://alpha6ops.com/claims/';
const policy = 'http://schemas.openid.net/pape/policies/2007/06/multi-factor';
const old = new Date(Date.now() - 60 * 60 * 1000).toISOString();
const fresh = new Date(Date.now() - 1000).toISOString();
const event = (methods = []) => ({
  user: { email: 'pilot@example.test', email_verified: true, name: 'Pilot', enrolledFactors: [] },
  request: { query: {} }, authentication: { methods }
});
const mock = () => {
  const calls = [], id = {}, access = {};
  return { calls, id, access, api: {
    authentication: {
      challengeWithAny: factors => calls.push(['challenge', factors]),
      enrollWithAny: factors => calls.push(['enroll', factors])
    },
    idToken: { setCustomClaim: (name, value) => { id[name] = value; } },
    accessToken: { setCustomClaim: (name, value) => { access[name] = value; } }
  } };
};
(async () => {
  const ordinary = mock();
  await security.onExecutePostLogin(event(), ordinary.api);
  assert.equal(ordinary.calls.length, 0, 'Personal login does not force MFA');

  const stepup = event(); stepup.request.query.acr_values = policy;
  const enrollment = mock();
  await security.onExecutePostLogin(stepup, enrollment.api);
  assert.equal(enrollment.calls[0][0], 'enroll', 'Requested step-up enrolls an unenrolled account');

  stepup.user.enrolledFactors = [{ type: 'otp' }];
  const challenge = mock();
  await security.onExecutePostLogin(stepup, challenge.api);
  assert.deepEqual(challenge.calls, [['challenge', [{ type: 'otp' }]]], 'Only an available factor is challenged');

  for (const method of ['mfa', 'passkey']) {
    const request = event([{ name: method, timestamp: fresh }]); request.request.query.acr_values = policy;
    const result = mock();
    await security.onExecutePostLogin(request, result.api);
    assert.equal(result.calls.length, 0, 'Fresh completed provider proof avoids a duplicate challenge');
    await claimsAction.onExecutePostLogin(request, result.api);
    assert.equal(result.id[prefix + 'mfa'], true);
    assert.equal(result.id[prefix + 'mfa_at'], Math.floor(Date.parse(fresh) / 1000));
    assert.deepEqual(result.id, result.access, 'ID and access tokens receive identical trusted claims');
  }
  for (const method of ['pwdless', 'federated', 'email_otp', 'pwd']) {
    const result = mock();
    await claimsAction.onExecutePostLogin(event([{ name: method, timestamp: fresh }]), result.api);
    assert.equal(result.id[prefix + 'mfa'], false, `${method} does not prove MFA`);
    assert.equal(result.id[prefix + 'mfa_at'], undefined);
  }
  const refresh = mock();
  await claimsAction.onExecutePostLogin(event([{ name: 'mfa', timestamp: old }]), refresh.api);
  assert.equal(refresh.id[prefix + 'mfa_at'], Math.floor(Date.parse(old) / 1000), 'Refreshing does not renew proof age');
  const staleRequest = event([{ name: 'mfa', timestamp: old }]); staleRequest.request.query.acr_values = policy;
  const stale = mock(); await security.onExecutePostLogin(staleRequest, stale.api);
  assert.equal(stale.calls.length, 1, 'Old MFA must be refreshed for a security check');
  const missing = mock(); const missingEvent = event(); delete missingEvent.authentication;
  await claimsAction.onExecutePostLogin(missingEvent, missing.api);
  assert.equal(missing.id[prefix + 'mfa'], false, 'Missing authentication methods fail closed');
  const future = mock();
  await claimsAction.onExecutePostLogin(event([{ name: 'mfa', timestamp: new Date(Date.now() + 60000).toISOString() }]), future.api);
  assert.equal(future.id[prefix + 'mfa'], false, 'Future proof is rejected');
  console.log('PASS Auth0 Actions: challenges, enrollment, actual proof, timestamps, refresh, and fail-closed claims.');
})().catch(error => { console.error(error); process.exitCode = 1; });
