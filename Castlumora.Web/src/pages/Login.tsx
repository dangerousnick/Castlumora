import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import type { FormEvent } from 'react';
import { ArrowRight, LockKeyhole, Server, ShieldCheck } from 'lucide-react';
import { Api, integratedServer } from '../api/client';
import Setup from '../setup/Setup';
import type { Session, User } from '../api/types';
import { errorText } from '../state';
export default function Login({ onLogin }: { onLogin: (s: Session) => void }) {
  const [server, setServer] = useState(
    localStorage.getItem('castlumora-server') ?? integratedServer(window.location.href),
  );
  const [autoConnect, setAutoConnect] = useState(Boolean(server));
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [profiles, setProfiles] = useState<User[]>([]);
  const [connected, setConnected] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [needsSetup, setNeedsSetup] = useState(false);
  useEffect(() => {
    if (!autoConnect) return;
    let active = true;
    setBusy(true);
    void Promise.resolve()
      .then(() => {
        const api = new Api(server);
        return api
          .publicInfo()
          .then(
            async (info) =>
              [info, info.StartupWizardCompleted ? await api.profiles() : []] as const,
          );
      })
      .then(([info, profiles]) => {
        if (!active) return;
        if (!info.StartupWizardCompleted) {
          setNeedsSetup(true);
          return;
        }
        setConnected(info.ServerName);
        setProfiles(profiles);
      })
      .catch((error) => {
        if (active) setError(errorText(error));
      })
      .finally(() => {
        if (active) setBusy(false);
      });
    return () => {
      active = false;
    };
  }, [autoConnect, server]);
  async function submit(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      const api = new Api(server);
      if (!connected) {
        const info = await api.publicInfo();
        if (!info.StartupWizardCompleted) {
          setNeedsSetup(true);
          return;
        }
        setConnected(info.ServerName);
        localStorage.setItem('castlumora-server', api.server);
        setProfiles(await api.profiles());
      } else {
        onLogin(await api.login(name, password));
        setPassword('');
      }
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  if (needsSetup)
    return (
      <Setup
        server={server}
        back={() => {
          setNeedsSetup(false);
          setAutoConnect(false);
        }}
        completed={() => {
          localStorage.setItem('castlumora-server', server);
          setNeedsSetup(false);
          setAutoConnect(false);
          void new Api(server)
            .publicInfo()
            .then(async (info) => {
              setConnected(info.ServerName);
              setProfiles(await new Api(server).profiles());
            })
            .catch((error) => setError(errorText(error)));
        }}
      />
    );
  return (
    <main className="login-page">
      <div className="login-story">
        <LinkBrand />
        <span className="eyebrow">WELCOME TO YOUR WORLD</span>
        <h1>
          Great stories.
          <br />
          Your castle.
        </h1>
        <p>
          All the films you love. The shows you can’t leave.
          <br />
          One place that’s entirely yours.
        </p>
        <div className="login-values">
          <span>
            <ShieldCheck size={18} />
            Your media, your control
          </span>
          <span>
            <LockKeyhole size={18} />
            Private by design
          </span>
        </div>
        <div className="castle-glow">
          <img src="./castle.svg" alt="" />
        </div>
      </div>
      <section className="login-panel">
        <form onSubmit={(e) => void submit(e)}>
          <span className="eyebrow">CASTLUMORA WEB · 1.0 DEVELOPMENT</span>
          <h2>{connected ? 'Welcome home' : 'Connect to your castle'}</h2>
          <p>
            {connected
              ? `Sign in to ${connected}.`
              : 'Enter the address of your Castlumora server.'}
          </p>
          {!connected ? (
            <label>
              <Server size={16} /> Server address
              <input
                type="url"
                required
                placeholder="https://media.example.com"
                value={server}
                onChange={(e) => {
                  setAutoConnect(false);
                  setBusy(false);
                  setServer(e.target.value);
                }}
                autoComplete="url"
              />
            </label>
          ) : (
            <>
              <div className="profiles">
                {profiles.map((p) => (
                  <button
                    type="button"
                    key={p.Id}
                    onClick={() => setName(p.Name)}
                    className={name === p.Name ? 'selected' : ''}
                  >
                    <span>{p.Name.slice(0, 1)}</span>
                    {p.Name}
                  </button>
                ))}
              </div>
              <label>
                Username
                <input
                  autoComplete="username"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </label>
              <label>
                Password
                <input
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </label>
            </>
          )}
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          <button disabled={busy} className="primary">
            {busy ? 'Connecting…' : connected ? 'Sign in' : 'Connect'}
            <ArrowRight size={18} />
          </button>
          {connected && (
            <button
              className="text-button"
              type="button"
              onClick={() => {
                setAutoConnect(false);
                setConnected('');
                setProfiles([]);
                setPassword('');
              }}
            >
              Change server
            </button>
          )}
          <Link to="/register">Create an account / use an invitation</Link>
          <small>
            Free, open source, and made for your media.
            <br />
            Castlumora is built on the Jellyfin server. GPL licensed.
          </small>
        </form>
      </section>
    </main>
  );
}
export function LinkBrand() {
  return (
    <div className="brand">
      <img src="./castle.svg" alt="" />
      <span>
        CASTLUMORA<small>YOUR STORIES LIVE HERE</small>
      </span>
    </div>
  );
}
