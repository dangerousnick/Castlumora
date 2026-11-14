import { useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronLeft, ChevronRight, MoreHorizontal, Play, Plus, Check } from 'lucide-react';
import type { Item } from '../api/types';
import { sameId } from '../api/client';
import { errorText, useApp } from '../state';
export function Feedback({ busy, error }: { busy?: boolean; error?: string }) {
  return busy ? (
    <p role="status" className="loading">
      Opening the castle gates…
    </p>
  ) : error ? (
    <p role="alert" className="error">
      {error}
    </p>
  ) : null;
}
export function Poster({ item, resume = false }: { item: Item; resume?: boolean }) {
  const { api, preferences, setPreferences } = useApp();
  const [error, setError] = useState('');
  const image = api.image(item);
  const listed = preferences.watchlist.some((id) => sameId(id, item.Id));
  async function preference(kind: 'watchlist' | 'hiddenResume', id: string, enabled: boolean) {
    try {
      setPreferences(await api.preference(kind, id, enabled));
      setError('');
    } catch (e) {
      setError(errorText(e));
    }
  }
  return (
    <article className="poster">
      <Link to={'/item/' + item.Id} className="poster-art" aria-label={'Details for ' + item.Name}>
        {image ? (
          <img src={image} alt="" loading="lazy" />
        ) : (
          <div className="poster-placeholder">
            <img src="./castle.svg" alt="" />
            <span>{item.Name}</span>
          </div>
        )}
        <span className="poster-hover">
          <Play size={24} />
        </span>
        {resume && (
          <progress
            aria-label="Playback progress"
            max={100}
            value={item.UserData?.PlayedPercentage ?? 0}
          />
        )}
      </Link>
      <div className="poster-caption">
        <div>
          <Link to={'/item/' + item.Id}>{item.SeriesName ?? item.Name}</Link>
          <small>
            {item.SeriesName
              ? `S${item.ParentIndexNumber ?? 1} · E${item.IndexNumber ?? 1} — ${item.Name}`
              : (item.ProductionYear ?? item.Type)}
          </small>
        </div>
        <details className="item-menu">
          <summary aria-label={'Options for ' + item.Name}>
            <MoreHorizontal size={18} />
          </summary>
          <div className="menu">
            <button onClick={() => void preference('watchlist', item.Id, !listed)}>
              {listed ? <Check size={14} /> : <Plus size={14} />}{' '}
              {listed ? 'Remove from My List' : 'Add to My List'}
            </button>
            {resume && (
              <button
                onClick={() => void preference('hiddenResume', item.SeriesId ?? item.Id, true)}
              >
                Remove from Continue Watching
              </button>
            )}
          </div>
        </details>
      </div>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </article>
  );
}
export function MediaRow({
  title,
  items,
  resume = false,
}: {
  title: string;
  items: Item[];
  resume?: boolean;
}) {
  const ref = useRef<HTMLDivElement>(null);
  if (!items.length) return null;
  return (
    <section className="media-row">
      <div className="section-heading">
        <h2>{title}</h2>
        <div>
          <button
            className="icon-button"
            aria-label={'Scroll ' + title + ' left'}
            onClick={() => ref.current?.scrollBy({ left: -600, behavior: 'smooth' })}
          >
            <ChevronLeft size={18} />
          </button>
          <button
            className="icon-button"
            aria-label={'Scroll ' + title + ' right'}
            onClick={() => ref.current?.scrollBy({ left: 600, behavior: 'smooth' })}
          >
            <ChevronRight size={18} />
          </button>
        </div>
      </div>
      <div
        className="poster-track"
        ref={ref}
        tabIndex={0}
        aria-label={title}
        onKeyDown={(event) => {
          if (event.target !== event.currentTarget) return;
          if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
            event.preventDefault();
            ref.current?.scrollBy({
              left: event.key === 'ArrowRight' ? 240 : -240,
              behavior: 'smooth',
            });
          }
        }}
      >
        {items.map((item) => (
          <Poster key={item.Id} item={item} resume={resume} />
        ))}
      </div>
    </section>
  );
}
export function Hero({ item }: { item: Item }) {
  const { api } = useApp();
  const image = api.image(item, 'Backdrop', 1920);
  return (
    <section
      className="hero"
      style={
        image
          ? {
              backgroundImage: `linear-gradient(90deg,rgba(13,11,20,.96),rgba(13,11,20,.3)),linear-gradient(0deg,var(--background),transparent 70%),url("${image}")`,
            }
          : undefined
      }
    >
      <div className="hero-copy">
        <span className="eyebrow">YOUR OWN WORLD OF ENTERTAINMENT</span>
        <div className="metadata">
          <span className="pill">{item.Type === 'Series' ? 'SERIES' : 'FEATURED'}</span>
          <span>{item.ProductionYear}</span>
          <span>{item.OfficialRating}</span>
          {item.CommunityRating && <span>★ {item.CommunityRating.toFixed(1)}</span>}
        </div>
        <h1>{item.Name}</h1>
        <p>{item.Overview || 'Make yourself at home. Your next great story is waiting.'}</p>
        <div className="actions">
          <Link
            className="primary button"
            to={item.Type === 'Series' ? '/item/' + item.Id : '/play/' + item.Id}
          >
            <Play size={18} fill="currentColor" />
            {item.Type === 'Series'
              ? 'Explore series'
              : item.UserData?.PlaybackPositionTicks
                ? 'Continue watching'
                : 'Watch now'}
          </Link>
          <Link className="button secondary" to={'/item/' + item.Id}>
            View details
          </Link>
        </div>
      </div>
      <span className="hero-note">YOUR SERVER. YOUR STORIES.</span>
    </section>
  );
}
