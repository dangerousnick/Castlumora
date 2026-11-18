import { useState } from 'react';
import { useApp } from '../state';
import { ActionStatus, useAction } from '../admin/Controls';

export default function AccountPassword({ accountOwner }: { accountOwner: boolean }) {
  const { api, session } = useApp();
  const [hasPassword, setHasPassword] = useState(session.user.HasPassword !== false);
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const action = useAction();
  if (!accountOwner)
    return (
      <p>
        This viewing profile uses its owning account’s password. Switch to that account to manage
        its password.
      </p>
    );
  if (
    !session.user.Policy?.IsAdministrator &&
    session.user.Policy?.EnableUserPreferenceAccess === false
  )
    return (
      <p>
        Account password changes are disabled by your server permissions. Contact the administrator.
      </p>
    );
  const valid =
    (!hasPassword || current.length > 0) &&
    current.length <= 1024 &&
    next.length >= 8 &&
    next.length <= 1024 &&
    next === confirmation;
  return (
    <form
      className="panel settings-fields"
      aria-label="Change account password"
      onSubmit={(event) => {
        event.preventDefault();
        if (!valid || action.busy) return;
        void action.run(async () => {
          try {
            // Explicit self ID also makes inherited administrator self-changes verify CurrentPw.
            await api.request('Users/' + encodeURIComponent(session.user.Id) + '/Password', {
              method: 'POST',
              body: JSON.stringify({ CurrentPw: current, NewPw: next, ResetPassword: false }),
            });
            session.user.HasPassword = true;
            setHasPassword(true);
          } finally {
            setCurrent('');
            setNext('');
            setConfirmation('');
          }
        }, 'Password updated. This session remains signed in; other sessions for this identity were revoked.');
      }}
    >
      <h2>Account security</h2>
      <p>
        Change the password for your signed-in account. Other sign-ins for this identity are ended;
        managed viewing profiles have separate sessions.
      </p>
      <p>Castlumora does not save these password fields in browser storage.</p>
      {!hasPassword && (
        <p>This account has no password. Leave the current password blank to set one.</p>
      )}
      <label>
        Current account password
        <input
          type="password"
          autoComplete="current-password"
          required={hasPassword}
          maxLength={1024}
          value={current}
          disabled={action.busy}
          onChange={(event) => setCurrent(event.target.value)}
        />
      </label>
      <label>
        New account password
        <input
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          maxLength={1024}
          value={next}
          disabled={action.busy}
          onChange={(event) => setNext(event.target.value)}
        />
      </label>
      <label>
        Confirm new account password
        <input
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          maxLength={1024}
          value={confirmation}
          disabled={action.busy}
          onChange={(event) => setConfirmation(event.target.value)}
        />
      </label>
      {confirmation && next !== confirmation && <p role="alert">The new passwords must match.</p>}
      <button disabled={action.busy || !valid}>Update account password</button>
      <ActionStatus message={action.message} />
    </form>
  );
}
