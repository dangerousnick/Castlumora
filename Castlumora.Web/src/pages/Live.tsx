import { Link } from 'react-router-dom';
import { Radio, Play } from 'lucide-react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
export default function Live() {
  const { api } = useApp();
  const result = useLoad(async () => {
    const channels = await api.channels();
    const programs = await api.programs();
    return { channels: channels.Items, programs: programs.Items };
  }, [api]);
  return (
    <main className="page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">
            <Radio size={15} /> HAPPENING RIGHT NOW
          </span>
          <h1>Live from your world</h1>
          <p>Your channels. Your front-row seat.</p>
        </div>
        <a
          className="button secondary"
          href={api.server + '/web/#/livetv.html'}
          target="_blank"
          rel="noopener noreferrer"
        >
          Recordings & full guide ↗
        </a>
      </div>
      <Feedback busy={result.busy} error={result.error} />
      {result.value?.channels.length === 0 && (
        <div className="empty">
          <h2>No channels available</h2>
          <p>
            Configure tuners and guide providers through the existing administration interface, or
            check profile permissions.
          </p>
        </div>
      )}
      <div className="guide">
        {result.value?.channels.map((channel) => (
          <article key={channel.Id}>
            <Link to={'/play/' + channel.Id} className="channel">
              <div>{api.image(channel) ? <img src={api.image(channel)} alt="" /> : <Radio />}</div>
              <span>
                <strong>{channel.Name}</strong>
                <small>
                  <Play size={12} />
                  Watch live
                </small>
              </span>
            </Link>
            <div className="programs">
              {result
                .value!.programs.filter((p) => p.ChannelId === channel.Id)
                .slice(0, 4)
                .map((p) => (
                  <details key={p.Id}>
                    <summary>
                      <small>
                        {p.StartDate &&
                          new Date(p.StartDate).toLocaleTimeString([], {
                            hour: '2-digit',
                            minute: '2-digit',
                          })}
                      </small>
                      <strong>{p.Name}</strong>
                    </summary>
                    <p>{p.Overview ?? 'No program description available.'}</p>
                    <Link to={'/play/' + channel.Id}>Watch channel</Link>
                  </details>
                ))}
            </div>
          </article>
        ))}
      </div>
    </main>
  );
}
