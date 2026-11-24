import { useState } from 'react';
import MediaPreferences from './MediaPreferences';
import AccountPassword from './AccountPassword';
import AccountSessions from './AccountSessions';
import { sameId } from '../api/client';
import { useNavigate } from 'react-router-dom';
import { useApp, useLoad } from '../state';
import type { User } from '../api/types';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from '../admin/Controls';

export default function Profiles() {
  const { api, session, replaceSession } = useApp();
  const navigate = useNavigate();
  const [password, setPassword] = useState('');
  const [target, setTarget] = useState('');
  const [deviceProfile, setDeviceProfile] = useState('');
  const action = useAction();
  const familyAction = useAction();
  const [sessionRevision, setSessionRevision] = useState(0);
  const profiles = useLoad(
    () =>
      api.request<{
        accountId: string;
        accountName: string;
        profiles: { id: string; name: string; preset: string; ready: boolean }[];
      }>('Castlumora/Profiles'),
    [api],
  );
  return (
    <main className="page">
      <h1>Viewing profiles</h1>
      <MediaPreferences />
      <AccountSessions key={sessionRevision} />
      <Feedback busy={profiles.busy} error={profiles.error} />
      {profiles.value && (
        <>
          {profiles.value.accountId && (
            <AccountPassword accountOwner={sameId(profiles.value.accountId, session.user.Id)} />
          )}
          {profiles.value.accountId && sameId(profiles.value.accountId, session.user.Id) && (
            <section className="panel" aria-label="Viewing profile devices">
              <h2>Viewing profile devices</h2>
              <p>
                Sign out every viewing profile and your other account devices. This account device
                stays signed in.
              </p>
              <button
                type="button"
                disabled={familyAction.busy}
                onClick={() => {
                  if (
                    !window.confirm(
                      'Sign out all viewing profiles and other account devices? Your current account session stays signed in.',
                    )
                  )
                    return;
                  void familyAction.run(async () => {
                    await api.request('Castlumora/Account/ProfileSessions/RevokeOthers', {
                      method: 'POST',
                      body: '{}',
                    });
                    setDeviceProfile('');
                    setSessionRevision((revision) => revision + 1);
                  }, 'Other account and viewing-profile devices signed out. This device remains signed in.');
                }}
              >
                Sign out other account and profile devices
              </button>
              <ActionStatus message={familyAction.message} />
              <label>
                Manage profile sign-ins
                <select
                  value={deviceProfile}
                  onChange={(event) => setDeviceProfile(event.target.value)}
                >
                  <option value="">Choose a viewing profile</option>
                  {profiles.value.profiles
                    .filter((profile) => !sameId(profile.id, profiles.value!.accountId))
                    .map((profile) => (
                      <option key={profile.id} value={profile.id}>
                        {profile.name}
                        {!profile.ready ? ' — unavailable' : ''}
                      </option>
                    ))}
                </select>
              </label>
              {profiles.value.profiles
                .filter(
                  (profile) =>
                    profile.id === deviceProfile && !sameId(profile.id, profiles.value!.accountId),
                )
                .map((profile) => (
                  <AccountSessions key={profile.id} profile={profile} />
                ))}
            </section>
          )}
          <p>
            Account: {profiles.value.accountName}. Each profile has separate watch progress, lists
            and preferences.
          </p>
          <p>
            Enter the account password for every switch. The previous profile session is revoked; no
            parent token is retained.
          </p>
          <form
            className="panel settings-fields"
            onSubmit={(event) => {
              event.preventDefault();
              void action.run(async () => {
                if (!replaceSession)
                  throw new Error('Profile switching is unavailable in this client.');
                const result = await api.request<{ AccessToken: string; User: User }>(
                  'Castlumora/Profiles/Switch',
                  {
                    method: 'POST',
                    body: JSON.stringify({ profileId: target, accountPassword: password }),
                  },
                );
                setPassword('');
                replaceSession({
                  server: session.server,
                  token: result.AccessToken,
                  profileName:
                    profiles.value?.profiles.find((profile) => profile.id === target)?.name ||
                    result.User.Name,
                  user: result.User,
                  deviceId: session.deviceId,
                });
                navigate('/');
              });
            }}
          >
            <label>
              Viewing profile
              <select required value={target} onChange={(event) => setTarget(event.target.value)}>
                <option value="">Choose a profile</option>
                {profiles.value.profiles.map((profile) => (
                  <option key={profile.id} value={profile.id} disabled={!profile.ready}>
                    {profile.name} ({profile.preset}){!profile.ready ? ' — unavailable' : ''}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Account password
              <input
                type="password"
                required
                autoComplete="current-password"
                maxLength={1024}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </label>
            <button disabled={action.busy || !target || !password}>Switch viewing profile</button>
            <ActionStatus message={action.message} />
          </form>
          <p>Ask the server administrator to create or adjust profiles in Users & devices.</p>
        </>
      )}
    </main>
  );
}
