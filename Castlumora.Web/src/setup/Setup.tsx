import { useState } from 'react';
import type { FormEvent } from 'react';
import { Api, ApiError } from '../api/client';
import { errorText } from '../state';
import { Feedback } from '../components/Media';
export default function Setup({
  server,
  completed,
  back,
}: {
  server: string;
  completed: () => void;
  back: () => void;
}) {
  const [name, setName] = useState('Castlumora');
  const [user, setUser] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [configured, setConfigured] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      const api = new Api(server);
      if (!configured) {
        if (password !== confirm) throw new Error('Passwords must match.');
        const info = await api.publicInfo();
        if (info.StartupWizardCompleted) {
          completed();
          return;
        }
        await api.request('Startup/User');
        await api.request('Startup/Configuration', {
          method: 'POST',
          body: JSON.stringify({
            ServerName: name,
            UICulture: 'en-US',
            MetadataCountryCode: 'US',
            PreferredMetadataLanguage: 'en',
          }),
        });
        await api.request('Startup/RemoteAccess', {
          method: 'POST',
          body: JSON.stringify({ EnableRemoteAccess: false }),
        });
        try {
          await api.request('Startup/User', {
            method: 'POST',
            body: JSON.stringify({ Name: user, Password: password }),
          });
        } catch (error) {
          if (error instanceof ApiError && error.status === 403) {
            setConfigured(true);
            setPassword('');
            setConfirm('');
            throw new Error(
              'An administrator password is already configured. Finish setup to sign in with the existing credentials.',
            );
          }
          throw error;
        }
        setConfigured(true);
        setPassword('');
        setConfirm('');
      }
      await api.request('Startup/Complete', { method: 'POST' });
      completed();
    } catch (error) {
      setError(errorText(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <main className="login-page">
      <div className="login-story">
        <img src="./castle.svg" alt="Castlumora castle" width="120" />
        <h1>Build your castle.</h1>
        <p>
          Create the first administrator for this fresh server. Libraries and additional users can
          be added after signing in.
        </p>
        <p>
          This preview stays on your computer unless you explicitly change its network settings.
        </p>
      </div>
      <section className="login-panel">
        <form onSubmit={(event) => void submit(event)}>
          <h2>Set up Castlumora</h2>
          <p>{server}</p>
          {!configured && (
            <>
              <label>
                Server name
                <input
                  required
                  value={name}
                  maxLength={120}
                  onChange={(event) => setName(event.target.value)}
                />
              </label>
              <label>
                Administrator username
                <input
                  required
                  autoComplete="username"
                  value={user}
                  maxLength={128}
                  onChange={(event) => setUser(event.target.value)}
                />
              </label>
              <label>
                Administrator password
                <input
                  required
                  type="password"
                  minLength={8}
                  maxLength={1024}
                  autoComplete="new-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                />
              </label>
              <label>
                Confirm administrator password
                <input
                  required
                  type="password"
                  autoComplete="new-password"
                  value={confirm}
                  onChange={(event) => setConfirm(event.target.value)}
                />
              </label>
            </>
          )}
          <Feedback busy={busy} error={error} />
          <button className="primary" disabled={busy}>
            {configured ? 'Finish setup' : 'Create administrator and finish setup'}
          </button>
          <button type="button" onClick={back} disabled={busy}>
            Change server
          </button>
          <p>
            Family, creator and business capabilities use the same free software. Named setup
            presets remain in development.
          </p>
        </form>
      </section>
    </main>
  );
}
