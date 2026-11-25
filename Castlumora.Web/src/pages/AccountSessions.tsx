import { useState } from 'react';
import { ApiError } from '../api/client';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { ActionStatus, useAction } from '../admin/Controls';

type SignIn = {
  id: number;
  deviceName: string;
  appName: string;
  appVersion: string;
  lastActive: string;
  current: boolean;
};
type Page = { identityId: string; total: number; sessions: SignIn[] };
export default function AccountSessions({
  profile,
}: { profile?: { id: string; name: string } } = {}) {
  const base = profile
    ? 'Castlumora/Account/ProfileSessions/' + encodeURIComponent(profile.id)
    : 'Castlumora/Account/Sessions';
  const heading = profile ? profile.name + ' signed-in devices' : 'My signed-in devices';
  const { api } = useApp();
  const [offset, setOffset] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const action = useAction();
  const size = 20;
  const inventory = useLoad(async () => {
    try {
      const value = await api.request<Page>(base, {}, { startIndex: offset, limit: size });
      if (
        !value ||
        !Number.isSafeInteger(value.total) ||
        value.total < 0 ||
        !Array.isArray(value.sessions) ||
        value.sessions.length > size ||
        value.sessions.some(
          (row) =>
            !row ||
            typeof row !== 'object' ||
            !Number.isSafeInteger(row.id) ||
            row.id < 1 ||
            typeof row.deviceName !== 'string' ||
            typeof row.appName !== 'string' ||
            typeof row.appVersion !== 'string' ||
            typeof row.lastActive !== 'string' ||
            !Number.isFinite(Date.parse(row.lastActive)) ||
            typeof row.current !== 'boolean',
        )
      )
        throw new Error('Invalid server sign-in inventory.');
      return value;
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        if (!profile) return null;
        throw new Error('Profile sign-ins are unavailable. Refresh your profile list.');
      }
      throw error;
    }
  }, [api, base, offset, refresh]);
  const reload = () => {
    setOffset(0);
    setRefresh((value) => value + 1);
  };
  return (
    <section className="panel" aria-label={heading}>
      <h2>{heading}</h2>
      <p>
        {profile
          ? 'Manage only this viewing profile’s saved sign-ins. Your account and other profiles stay signed in. Profile history, preferences and credentials are preserved.'
          : 'Only sign-ins for this account or viewing profile are shown. Other identities are managed separately. Saved sign-ins can be revoked even when a device is offline.'}
      </p>
      <Feedback busy={inventory.busy} error={inventory.error} />
      {inventory.value === null && (
        <p>
          Device management requires a newer Castlumora server. You can still use Sign out or
          contact the administrator.
        </p>
      )}
      {inventory.value && (
        <>
          <button
            disabled={action.busy || inventory.busy || inventory.value.total <= (profile ? 0 : 1)}
            onClick={() => {
              if (
                !window.confirm(
                  profile
                    ? 'Sign out every device for this viewing profile? Your account and other profiles stay signed in.'
                    : 'Sign out all other devices for this identity? This device stays signed in; other accounts and viewing profiles are unaffected.',
                )
              )
                return;
              void action.run(
                async () => {
                  await api.request(base + (profile ? '/RevokeAll' : '/RevokeOthers'), {
                    method: 'POST',
                    body: '{}',
                  });
                  reload();
                },
                profile
                  ? 'Viewing profile signed out on all devices. Your account remains signed in.'
                  : 'Other sign-ins for this identity were revoked. This device remains signed in.',
              );
            }}
          >
            {profile ? 'Sign out all profile devices' : 'Sign out other devices'}
          </button>
          {inventory.value.sessions.length === 0 && <p>No saved sign-ins on this page.</p>}
          <div className="admin-list">
            {inventory.value.sessions.map((row) => (
              <article key={row.id} aria-label={row.deviceName + ' · ' + row.appName}>
                <span>
                  <strong>{row.deviceName}</strong>
                  <small>
                    {row.appName} {row.appVersion} · Last active:{' '}
                    {row.lastActive ? new Date(row.lastActive).toLocaleString() : 'Unknown'}
                  </small>
                </span>
                {row.current ? (
                  <span>Current sign-in</span>
                ) : (
                  <button
                    disabled={action.busy}
                    onClick={() => {
                      if (
                        !window.confirm(
                          'Revoke this saved sign-in? Its next request must authenticate again.',
                        )
                      )
                        return;
                      void action.run(async () => {
                        await api.request(base + '/' + row.id, {
                          method: 'DELETE',
                        });
                        reload();
                      }, 'Saved sign-in revoked.');
                    }}
                  >
                    Sign out this device
                  </button>
                )}
              </article>
            ))}
          </div>
          {(inventory.value.total > size || offset > 0) && (
            <div className="filters" aria-label="Sign-in pages">
              <button
                disabled={action.busy || inventory.busy || offset === 0}
                onClick={() => setOffset(Math.max(0, offset - size))}
              >
                Previous devices
              </button>
              <span>
                {inventory.value.sessions.length
                  ? `${offset + 1}–${offset + inventory.value.sessions.length}`
                  : '0'}{' '}
                of {inventory.value.total}
              </span>
              <button
                disabled={
                  action.busy ||
                  inventory.busy ||
                  offset + inventory.value.sessions.length >= inventory.value.total
                }
                onClick={() => setOffset(offset + size)}
              >
                Next devices
              </button>
            </div>
          )}
        </>
      )}
      <ActionStatus message={action.message} />
    </section>
  );
}
