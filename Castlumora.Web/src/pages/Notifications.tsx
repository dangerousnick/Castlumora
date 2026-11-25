import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useApp, useLoad } from '../state';
import { ActionStatus, useAction } from '../admin/Controls';
import { Feedback } from '../components/Media';

export default function Notifications() {
  const { api } = useApp();
  const [revision, setRevision] = useState(0);
  const action = useAction();
  const state = useLoad(() => api.notifications(), [api, revision]);
  const refresh = () => setRevision((value) => value + 1);
  return (
    <main className="page">
      <h1>Notifications</h1>
      <p>New episodes from your followed series, private to this viewing profile.</p>
      <p>
        Opening or refreshing this page checks your library. Enabling establishes a baseline;
        existing episodes and newly followed shows do not produce historical alerts. Email, push and
        recording alerts are not available yet.
      </p>
      <Feedback busy={state.busy} error={state.error} />
      {state.value && (
        <>
          <div className="actions">
            <button
              disabled={action.busy || state.busy}
              onClick={() =>
                void action.run(
                  async () => {
                    await api.notificationSettings(!state.value!.enabled);
                    refresh();
                  },
                  state.value!.enabled
                    ? 'Episode notifications paused.'
                    : 'Episode notifications enabled.',
                )
              }
            >
              {state.value.enabled ? 'Pause episode notifications' : 'Enable episode notifications'}
            </button>
            <button disabled={action.busy || state.busy} onClick={refresh}>
              Refresh notifications
            </button>
          </div>
          <p>{state.value.unread} unread notifications</p>
          {!state.value.items.length && <p>No episode notifications are currently available.</p>}
          <ul>
            {state.value.items.map((item) => (
              <li key={item.id}>
                <Link to={'/item/' + item.id}>
                  {item.seriesName} — {item.title}
                </Link>
                <p>
                  {item.season != null ? `Season ${item.season}` : ''}
                  {item.episode != null ? ` · Episode ${item.episode}` : ''} ·{' '}
                  {item.read ? 'Read' : 'Unread'}
                </p>
                <div className="actions">
                  <button
                    disabled={action.busy}
                    onClick={() =>
                      void action.run(async () => {
                        await api.updateNotification(item.id, { read: !item.read });
                        refresh();
                      }, 'Notification updated.')
                    }
                  >
                    {item.read ? 'Mark unread' : 'Mark read'}
                  </button>
                  <button
                    disabled={action.busy}
                    onClick={() =>
                      void action.run(async () => {
                        await api.updateNotification(item.id, { dismissed: true });
                        refresh();
                      }, 'Notification dismissed.')
                    }
                  >
                    Dismiss notification
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
      <ActionStatus message={action.message} />
    </main>
  );
}
