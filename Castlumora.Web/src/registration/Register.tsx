import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Api } from '../api/client';
import { errorText } from '../state';
import { LinkBrand } from '../pages/Login';
import { registrationApi } from './api';
export default function Register() {
  const [parameters, setParameters] = useSearchParams();
  const [server, setServer] = useState(
    () => parameters.get('server') ?? localStorage.getItem('castlumora-server') ?? '',
  );
  const [invitation, setInvitation] = useState(() => parameters.get('invite') ?? '');
  const [connected, setConnected] = useState('');
  const [requiredInvite, setRequiredInvite] = useState(true);
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [created, setCreated] = useState(false);
  useEffect(() => {
    if (parameters.has('invite')) {
      const clean = new URLSearchParams(parameters);
      clean.delete('invite');
      setParameters(clean, { replace: true });
    }
  }, [parameters, setParameters]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      const api = new Api(server);
      if (!connected) {
        const [info, availability] = await Promise.all([
          api.publicInfo(),
          registrationApi(api).availability(),
        ]);
        if (!info.StartupWizardCompleted || !availability.enabled)
          throw new Error('This server is not accepting registration. Ask its administrator.');
        setRequiredInvite(availability.requireInvitation);
        setConnected(info.ServerName);
      } else {
        if (password !== confirmation) throw new Error('Passwords do not match.');
        await registrationApi(api).register(name, password, invitation);
        setPassword('');
        setConfirmation('');
        setInvitation('');
        setCreated(true);
        localStorage.setItem('castlumora-server', api.server);
      }
    } catch (error) {
      setError(errorText(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login-page">
      <section className="login-story">
        <LinkBrand />
        <h1>Your place in the castle.</h1>
        <p>
          Create a viewing account on the server you trust. Its administrator chooses your access.
        </p>
        <div className="castle-glow">
          <img src="./castle.svg" alt="" />
        </div>
      </section>
      <section className="login-panel">
        {created ? (
          <div>
            <h2>Your account is ready</h2>
            <p>
              Sign in with your new username and password. If approval is required, the
              administrator must enable the account first.
            </p>
            <Link className="button primary" to="/">
              Go to sign in
            </Link>
          </div>
        ) : (
          <form onSubmit={(event) => void submit(event)}>
            <span className="eyebrow">JOIN CASTLUMORA</span>
            <h2>{connected ? `Join ${connected}` : 'Choose your server'}</h2>
            <p>
              {connected ? server : 'Confirm the server address before entering account details.'}
            </p>
            {!connected ? (
              <label>
                Server address
                <input
                  type="url"
                  required
                  value={server}
                  onChange={(e) => setServer(e.target.value)}
                  placeholder="https://media.example.com"
                />
              </label>
            ) : (
              <>
                <label>
                  New username
                  <input
                    required
                    autoComplete="username"
                    maxLength={128}
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                  />
                </label>
                <label>
                  New password
                  <input
                    required
                    minLength={8}
                    maxLength={1024}
                    type="password"
                    autoComplete="new-password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                  />
                </label>
                <label>
                  Confirm new password
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    value={confirmation}
                    onChange={(e) => setConfirmation(e.target.value)}
                  />
                </label>
                <label>
                  Invitation code{!requiredInvite && ' (optional)'}
                  <input
                    required={requiredInvite}
                    autoComplete="off"
                    type="password"
                    value={invitation}
                    onChange={(e) => setInvitation(e.target.value)}
                  />
                </label>
              </>
            )}
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
            <button className="primary" disabled={busy}>
              {busy ? 'Please wait…' : connected ? 'Create account' : 'Check registration'}
            </button>
            {connected && (
              <button
                className="text-button"
                type="button"
                onClick={() => {
                  setConnected('');
                  setPassword('');
                  setConfirmation('');
                }}
              >
                Change server
              </button>
            )}
            <Link to="/">Back to sign in</Link>
            <small>Free & open source. Built on Jellyfin. GPL licensed.</small>
          </form>
        )}
      </section>
    </main>
  );
}
