import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useApp, errorText } from '../state';
import type { Item } from '../api/types';
import { authenticatedMediaUrl, ticks } from '../player/profile';
import { audioProfile } from './api';
type Repeat = 'off' | 'all' | 'one';
interface MusicState {
  queue: Item[];
  index: number;
  stop: () => void;
  play: (items: Item[], index?: number) => void;
  enqueue: (items: Item[]) => void;
}
const MusicContext = createContext<MusicState | null>(null);
export const useMusic = () => useContext(MusicContext);
export function MusicProvider({ children }: { children: ReactNode }) {
  const [queue, setQueue] = useState<Item[]>([]);
  const [index, setIndex] = useState(0);
  const [generation, setGeneration] = useState(0);
  const [volume, setVolume] = useState(1);
  const [repeat, setRepeat] = useState<Repeat>('off');
  const current = queue[index];
  const stop = useCallback(() => setQueue([]), []);
  function play(items: Item[], start = 0) {
    setQueue([...items]);
    setIndex(start);
    setGeneration((value) => value + 1);
  }
  function next() {
    if (index + 1 < queue.length) setIndex(index + 1);
    else if (repeat === 'all') play(queue, 0);
    else setQueue([]);
  }
  function shuffle() {
    const shuffled = [...queue];
    for (let i = shuffled.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    play(shuffled);
  }
  return (
    <MusicContext.Provider
      value={{
        queue,
        index,
        stop,
        play,
        enqueue: (items) =>
          queue.length ? setQueue((value) => [...value, ...items]) : play(items),
      }}
    >
      <div className={current ? 'with-audio' : ''}>
        {children}
        {current && (
          <MusicPlayer
            key={current.Id + '-' + index + '-' + generation}
            item={current}
            repeat={repeat}
            volume={volume}
            changeVolume={setVolume}
            ended={next}
            controls={
              <>
                <button disabled={index === 0} onClick={() => setIndex(index - 1)}>
                  Previous track
                </button>
                <button onClick={next}>Next track</button>
                <button onClick={shuffle}>Shuffle queue</button>
                <label>
                  Repeat
                  <select value={repeat} onChange={(e) => setRepeat(e.target.value as Repeat)}>
                    <option value="off">Off</option>
                    <option value="all">Queue</option>
                    <option value="one">Track</option>
                  </select>
                </label>
                <details>
                  <summary>Queue ({queue.length})</summary>
                  <ol>
                    {queue.map((track, position) => (
                      <li key={position}>
                        <button
                          aria-current={position === index ? 'true' : undefined}
                          onClick={() => setIndex(position)}
                        >
                          {track.Name}
                        </button>
                      </li>
                    ))}
                  </ol>
                </details>
                <button onClick={() => setQueue([])}>Close music</button>
              </>
            }
          />
        )}
      </div>
    </MusicContext.Provider>
  );
}
function MusicPlayer({
  item,
  repeat,
  volume,
  changeVolume,
  ended,
  controls,
}: {
  item: Item;
  repeat: Repeat;
  volume: number;
  changeVolume: (value: number) => void;
  ended: () => void;
  controls: ReactNode;
}) {
  const { api } = useApp();
  const ref = useRef<HTMLAudioElement>(null);
  const position = useRef(0);
  const resumeWanted = useRef(true);
  const [clock, setClock] = useState(0);
  const [paused, setPaused] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const [compatible, setCompatible] = useState(false);
  const [method, setMethod] = useState('');
  const direct = useRef(true);
  const latest = useRef({ repeat, ended });
  useEffect(() => {
    latest.current = { repeat, ended };
  }, [repeat, ended]);
  useEffect(() => {
    if (ref.current) ref.current.volume = volume;
  }, [volume]);
  const duration = (item.RunTimeTicks ?? 0) / 10000000;
  function seek(seconds: number) {
    if (ref.current?.readyState) resumeWanted.current = !ref.current.paused;
    position.current = ticks(Math.max(0, Math.min(seconds, duration)));
    setClock(position.current / 10000000);
    if (direct.current && ref.current?.readyState)
      ref.current.currentTime = position.current / 10000000;
    else setAttempt((value) => value + 1);
  }
  useEffect(() => {
    const audio = ref.current!;
    let cancelled = false;
    let session = '';
    let source = '';
    let playMethod = 'DirectPlay';
    let offset = 0;
    let started = false;
    let timer: ReturnType<typeof setInterval> | undefined;
    setLoading(true);
    setError('');
    const body = () => ({
      ItemId: item.Id,
      MediaSourceId: source,
      PlaySessionId: session,
      PositionTicks: position.current,
      IsPaused: audio.paused,
      CanSeek: true,
      PlayMethod: playMethod,
    });
    const update = () => {
      if (audio.readyState) {
        position.current = ticks(audio.currentTime) + offset;
        if (!cancelled) {
          setClock(position.current / 10000000);
          setPaused(audio.paused);
        }
      }
    };
    const report = async (kind: 'Playing' | 'Playing/Progress' | 'Playing/Stopped') => {
      try {
        await api.report(kind, body());
      } catch {
        if (!cancelled) setError('Music progress could not be saved.');
      }
    };
    const playing = () => {
      resumeWanted.current = true;
      setLoading(false);
      setPaused(false);
      if (!started) {
        started = true;
        void report('Playing');
      }
    };
    const changed = () => {
      update();
      if (started) void report('Playing/Progress');
    };
    const failed = () => {
      if (!cancelled) {
        setLoading(false);
        setError('This audio stream could not be played. Try the compatible stream.');
      }
    };
    const finished = () => {
      update();
      if (started) {
        void report('Playing/Stopped');
        started = false;
      }
      if (latest.current.repeat === 'one') {
        position.current = 0;
        setAttempt((value) => value + 1);
      } else latest.current.ended();
    };
    audio.addEventListener('timeupdate', update);
    audio.addEventListener('playing', playing);
    audio.addEventListener('pause', changed);
    audio.addEventListener('seeked', changed);
    audio.addEventListener('error', failed);
    audio.addEventListener('ended', finished);
    void (async () => {
      try {
        const start = position.current;
        const info = await api.playback(
          item.Id,
          audioProfile(),
          start,
          undefined,
          undefined,
          compatible,
          { maxBitrate: compatible ? 192000 : undefined },
        );
        session = info.PlaySessionId;
        const selected = info.MediaSources[0];
        if (info.ErrorCode || !selected) throw new Error('No playable audio source was returned.');
        source = selected.Id;
        if (cancelled) {
          await api.cleanup(session);
          return;
        }
        direct.current = !!selected.SupportsDirectPlay && !compatible;
        playMethod = direct.current
          ? 'DirectPlay'
          : selected.SupportsDirectStream && !compatible
            ? 'DirectStream'
            : 'Transcode';
        setMethod(playMethod);
        offset = direct.current ? 0 : start;
        const url = direct.current
          ? api.url(`Audio/${item.Id}/stream`, {
              Static: true,
              MediaSourceId: source,
              DeviceId: api.session!.deviceId,
              PlaySessionId: session,
            }).href
          : selected.TranscodingUrl;
        if (!url) throw new Error('The server returned no compatible audio URL.');
        audio.src = authenticatedMediaUrl(api, url);
        audio.onloadedmetadata = () => {
          if (direct.current && start) audio.currentTime = start / 10000000;
        };
        if (resumeWanted.current) await audio.play();
        else {
          audio.load();
          setPaused(true);
          setLoading(false);
        }
        if (cancelled) return;
        timer = setInterval(() => {
          update();
          if (started) void report('Playing/Progress');
        }, 10000);
      } catch (error) {
        if (!cancelled) {
          setError(errorText(error));
          setLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
      if (timer) clearInterval(timer);
      update();
      if (started) void api.report('Playing/Stopped', body()).catch(() => {});
      audio.removeEventListener('timeupdate', update);
      audio.removeEventListener('playing', playing);
      audio.removeEventListener('pause', changed);
      audio.removeEventListener('seeked', changed);
      audio.removeEventListener('error', failed);
      audio.removeEventListener('ended', finished);
      audio.pause();
      audio.onloadedmetadata = null;
      audio.removeAttribute('src');
      audio.load();
      if (session) void api.cleanup(session);
    };
  }, [api, item.Id, compatible, attempt]);
  return (
    <section className="audio-bar" aria-label="Music player">
      <audio ref={ref} aria-label={item.Name} />
      <div className="audio-title">
        <Link to={'/item/' + item.Id}>{item.Name}</Link>
        <small>
          {item.Artists?.join(', ')} {loading ? ' · Loading…' : ' · ' + method}
        </small>
      </div>
      <div className="audio-main-controls">
        <button
          onClick={() => {
            if (ref.current?.paused)
              void ref.current.play().catch((error) => setError(errorText(error)));
            else ref.current?.pause();
          }}
        >
          {paused ? 'Play music' : 'Pause music'}
        </button>
        <label className="audio-position">
          Music position {Math.floor(clock / 60)}:{String(Math.floor(clock % 60)).padStart(2, '0')}
          <input
            aria-label="Music position"
            type="range"
            min="0"
            max={duration || 1}
            step="1"
            value={Math.min(clock, duration || clock)}
            onChange={(e) => seek(Number(e.target.value))}
          />
        </label>
        <label>
          Volume
          <input
            aria-label="Music volume"
            type="range"
            min="0"
            max="1"
            step="0.05"
            value={volume}
            onChange={(e) => changeVolume(Number(e.target.value))}
          />
        </label>
        {controls}
        <button
          onClick={() => {
            position.current = ticks(clock);
            setCompatible(true);
            setAttempt((value) => value + 1);
          }}
        >
          Compatible audio
        </button>
      </div>
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </section>
  );
}
