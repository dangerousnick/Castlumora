import { lazy, Suspense } from 'react';
import { NavLink, Route, Routes } from 'react-router-dom';
import { useApp } from '../state';
import { Feedback } from '../components/Media';
const Registration = lazy(() => import('./Registration'));
const Skipper = lazy(() => import('./Skipper'));
const Presentation = lazy(() => import('../pages/Admin'));
const Libraries = lazy(() => import('./Libraries'));
const Metadata = lazy(() => import('./Metadata'));
const Users = lazy(() => import('./Users'));
const Encoding = lazy(() => import('./Encoding'));
const System = lazy(() => import('./System'));
const Plugins = lazy(() => import('./Plugins'));
const LiveSetup = lazy(() => import('./LiveSetup'));
export default function Administration() {
  const { api, session } = useApp();
  if (!session.user.Policy?.IsAdministrator)
    return (
      <main className="page">
        <Feedback error="Administrator access required." />
      </main>
    );
  return (
    <div className="administration">
      <aside>
        <span className="eyebrow">CONTROL ROOM</span>
        <nav aria-label="Administration sections">
          {[
            ['', 'Overview & presentation'],
            ['libraries', 'Libraries & metadata'],
            ['metadata', 'Metadata Management'],
            ['live', 'Live TV & DVR'],
            ['encoding', 'Playback & encoding'],
            ['skipper', 'Intro & outro skipper'],
            ['users', 'Users & devices'],
            ['registration', 'Registration & invitations'],
            ['plugins', 'Plugins'],
            ['system', 'System & backups'],
          ].map(([path, name]) => (
            <NavLink key={path} to={'/admin' + (path ? '/' + path : '')} end>
              {name}
            </NavLink>
          ))}
        </nav>
        <a href={api.legacy()} target="_blank" rel="noopener noreferrer">
          Existing dashboard ↗
        </a>
      </aside>
      <main className="admin-workspace">
        <Suspense fallback={<Feedback busy />}>
          <Routes>
            <Route index element={<Presentation />} />
            <Route path="libraries" element={<Libraries />} />
            <Route path="metadata" element={<Metadata />} />
            <Route path="live" element={<LiveSetup />} />
            <Route path="encoding" element={<Encoding />} />
            <Route path="skipper" element={<Skipper />} />
            <Route path="users" element={<Users />} />
            <Route path="registration" element={<Registration />} />
            <Route path="plugins" element={<Plugins />} />
            <Route path="system" element={<System />} />
            <Route path="*" element={<Feedback error="Administration section not found." />} />
          </Routes>
        </Suspense>
      </main>
    </div>
  );
}
