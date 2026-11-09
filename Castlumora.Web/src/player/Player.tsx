import { useEffect, useRef, useState } from 'react';
import { Link, useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import Hls from 'hls.js';
import type { MediaSource } from '../api/types';
import { errorText, useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { authenticatedMediaUrl, browserProfile, ticks } from './profile';
export default function Player() {
  const { id = '' } = useParams();
  const { api } = useApp();
  const videoRef = useRef<HTMLVideoElement>(null);
  const position = useRef<number | null>(null);
  const directMode = useRef(true);
  const resumeAfterChange = useRef(true);
  const [clock, setClock] = useState(0);
  const [seekDraft, setSeekDraft] = useState<number | null>(null);
  const [quality, setQuality] = useState(0);
  const [mediaVersion, setMediaVersion] = useState('');
  const [ended, setEnded] = useState(false);
  const navigate = useNavigate();
  const [audio, setAudio] = useState<number>();
  const [subtitle, setSubtitle] = useState(-1);
  const [transcode, setTranscode] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const [source, setSource] = useState<MediaSource>();
  const [error, setError] = useState('');
  const [reportError, setReportError] = useState('');
  const [loading, setLoading] = useState(true);
  const item = useLoad(() => api.item(id), [api, id]);
  const initial = item.value?.UserData?.PlaybackPositionTicks ?? 0;
  const next = useLoad(
    () => (item.value ? api.nextEpisode(item.value) : Promise.resolve(undefined)),
    [api, item.value],
  );
  const channels = useLoad(
    () => (item.value?.Type === 'TvChannel' ? api.channels() : Promise.resolve(undefined)),
    [api, item.value?.Type],
  );
  const duration = (item.value?.RunTimeTicks ?? 0) / 10000000;
  function changing() {
    if (videoRef.current?.readyState) resumeAfterChange.current = !videoRef.current.paused;
  }
  function seek(seconds: number) {
    const target = Math.max(0, Math.min(seconds, duration));
    position.current = ticks(target);
    setClock(target);
    setSeekDraft(null);
    if (directMode.current && videoRef.current?.readyState) {
      videoRef.current.currentTime = target;
    } else {
      changing();
      setAttempt((a) => a + 1);
    }
  }
  useEffect(() => {
    if (!item.value) return;
    const video = videoRef.current!;
    let cancelled = false;
    let hls: Hls | undefined;
    let playSession = '';
    let liveStream: string | undefined;
    let mediaSource = '';
    let started = false;
    let offset = 0;
    let direct = true;
    let method = 'DirectPlay';
    let timer: ReturnType<typeof setInterval> | undefined;
    setLoading(true);
    setError('');
    const start = position.current ?? initial;
    position.current = start;
    function body() {
      return {
        ItemId: id,
        MediaSourceId: mediaSource,
        PlaySessionId: playSession,
        PositionTicks: position.current ?? 0,
        IsPaused: video.paused,
        IsMuted: video.muted,
        CanSeek: item.value?.Type !== 'TvChannel',
        PlayMethod: method,
        AudioStreamIndex: audio,
        SubtitleStreamIndex: subtitle,
        LiveStreamId: liveStream,
      };
    }
    function updatePosition() {
      if (video.readyState > 0) {
        position.current = Math.min(
          ticks(video.currentTime) + offset,
          item.value?.RunTimeTicks ?? Number.MAX_SAFE_INTEGER,
        );
        if (!cancelled) setClock(position.current / 10000000);
      }
    }
    async function report(kind: 'Playing' | 'Playing/Progress' | 'Playing/Stopped') {
      try {
        await api.report(kind, body());
        setReportError('');
      } catch {
        setReportError('Playback progress could not be saved. Check your connection.');
      }
    }
    function playing() {
      setLoading(false);
      if (!started) {
        started = true;
        void report('Playing');
      } else void report('Playing/Progress');
    }
    function changed() {
      updatePosition();
      if (started) void report('Playing/Progress');
    }
    function finished() {
      updatePosition();
      setEnded(true);
    }
    function failed() {
      setLoading(false);
      setError(
        'This stream could not be played. Try the compatible stream, or use the existing client.',
      );
    }
    video.addEventListener('playing', playing);
    video.addEventListener('pause', changed);
    video.addEventListener('seeked', changed);
    video.addEventListener('timeupdate', updatePosition);
    video.addEventListener('error', failed);
    video.addEventListener('ended', finished);
    void (async () => {
      try {
        const info = await api.playback(
          id,
          browserProfile(),
          start,
          audio,
          subtitle,
          transcode || audio !== undefined || subtitle >= 0 || quality > 0,
          { mediaSourceId: mediaVersion || undefined, maxBitrate: quality || undefined },
        );
        playSession = info.PlaySessionId;
        if (info.ErrorCode || !info.MediaSources.length)
          throw new Error(info.ErrorCode ?? 'No playable media source was returned.');
        const selected = info.MediaSources[0];
        mediaSource = selected.Id;
        liveStream = selected.LiveStreamId;
        if (cancelled) {
          await api.cleanup(playSession, liveStream);
          return;
        }
        setSource(selected);
        direct =
          !!selected.SupportsDirectPlay &&
          !transcode &&
          audio === undefined &&
          subtitle < 0 &&
          quality === 0;
        directMode.current = direct;
        method = direct
          ? 'DirectPlay'
          : selected.SupportsDirectStream &&
              !transcode &&
              audio === undefined &&
              subtitle < 0 &&
              quality === 0
            ? 'DirectStream'
            : 'Transcode';
        let url: string;
        if (direct) {
          url = authenticatedMediaUrl(
            api,
            api.url(`Videos/${id}/stream`, {
              Static: true,
              MediaSourceId: mediaSource,
              DeviceId: api.session!.deviceId,
              PlaySessionId: playSession,
            }).href,
          );
        } else {
          if (!selected.TranscodingUrl)
            throw new Error('The server did not return a compatible streaming URL.');
          url = authenticatedMediaUrl(api, selected.TranscodingUrl);
          offset = start;
        }
        if (/\.m3u8(?:\?|$)/i.test(url) && !video.canPlayType('application/vnd.apple.mpegurl')) {
          if (!Hls.isSupported()) throw new Error('HLS is not supported in this browser.');
          hls = new Hls({
            xhrSetup(xhr, url) {
              const target = new URL(url, api.server + '/');
              if (target.origin === new URL(api.server).origin)
                xhr.setRequestHeader('X-Emby-Token', api.session!.token);
            },
          });
          hls.on(Hls.Events.ERROR, (_event, data) => {
            if (data.fatal) failed();
          });
          hls.loadSource(url);
          hls.attachMedia(video);
        } else video.src = url;
        video.onloadedmetadata = () => {
          if (direct && start > 0) video.currentTime = start / 10000000;
          setLoading(false);
        };
        if (subtitle >= 0) {
          const stream = selected.MediaStreams.find(
            (s) => s.Type === 'Subtitle' && s.Index === subtitle,
          );
          if (stream?.DeliveryUrl && stream.IsExternal) {
            const track = document.createElement('track');
            track.kind = 'subtitles';
            track.label = stream.DisplayTitle ?? stream.Language ?? 'Subtitles';
            track.srclang = stream.Language ?? 'en';
            track.src = authenticatedMediaUrl(api, stream.DeliveryUrl);
            track.default = true;
            video.appendChild(track);
          }
        }
        // Playback requires a user gesture on browsers that reject autoplay.
        if (resumeAfterChange.current) void video.play().catch(() => setLoading(false));
        timer = setInterval(() => {
          if (started && !video.paused) {
            updatePosition();
            void report('Playing/Progress');
          }
        }, 10000);
      } catch (e) {
        if (!cancelled) {
          setError(errorText(e));
          setLoading(false);
        }
      }
    })();
    return () => {
      cancelled = true;
      if (timer) clearInterval(timer);
      updatePosition();
      if (started) void api.report('Playing/Stopped', body()).catch(() => {});
      video.pause();
      video.removeEventListener('playing', playing);
      video.removeEventListener('pause', changed);
      video.removeEventListener('seeked', changed);
      video.removeEventListener('timeupdate', updatePosition);
      video.removeEventListener('error', failed);
      video.removeEventListener('ended', finished);
      video.onloadedmetadata = null;
      hls?.destroy();
      video.removeAttribute('src');
      video.querySelectorAll('track').forEach((t) => t.remove());
      video.load();
      if (playSession) void api.cleanup(playSession, liveStream);
    };
  }, [api, id, initial, item.value, audio, subtitle, transcode, attempt, quality, mediaVersion]);
  return (
    <main className="player-page">
      <div className="player-heading">
        <Link to={item.value?.Type === 'TvChannel' ? '/live' : '/item/' + id} className="back-link">
          <ArrowLeft size={20} />
          Back
        </Link>
        <h1>{item.value?.Name ?? 'Your screening room'}</h1>
        {item.value?.Type === 'TvChannel' && (
          <label>
            Switch channel
            <select value={id} onChange={(e) => navigate('/play/' + e.target.value)}>
              {channels.value?.Items.map((channel) => (
                <option key={channel.Id} value={channel.Id}>
                  {channel.Name}
                </option>
              ))}
            </select>
          </label>
        )}
      </div>
      <Feedback busy={item.busy} error={item.error} />
      <video
        ref={videoRef}
        controls
        playsInline
        crossOrigin="anonymous"
        aria-label={item.value?.Name ?? 'Video player'}
      />
      {item.value?.Type !== 'TvChannel' && duration > 0 && (
        <div className="playback-timeline">
          <label htmlFor="playback-position">
            {Math.floor(clock / 60)}:{String(Math.floor(clock % 60)).padStart(2, '0')} /{' '}
            {Math.floor(duration / 60)}:{String(Math.floor(duration % 60)).padStart(2, '0')}
          </label>
          <input
            id="playback-position"
            aria-label="Playback position"
            type="range"
            min={0}
            max={duration}
            step={1}
            value={seekDraft ?? clock}
            onChange={(e) => setSeekDraft(Number(e.target.value))}
            onPointerUp={(e) => seek(Number(e.currentTarget.value))}
            onKeyUp={(e) => seek(Number(e.currentTarget.value))}
          />
        </div>
      )}
      {next.value && (
        <div className="actions">
          <button
            className={ended ? 'primary' : ''}
            onClick={() => navigate('/play/' + next.value!.Id)}
          >
            Next episode: {next.value.Name}
          </button>
        </div>
      )}
      <Feedback busy={loading && !error} error={error} />
      {reportError && (
        <p role="status" className="notice">
          {reportError}
        </p>
      )}
      <div className="player-tools">
        <label>
          Quality
          <select
            value={quality}
            onChange={(e) => {
              changing();
              setQuality(Number(e.target.value));
            }}
          >
            <option value={0}>Auto / original</option>
            {[20000000, 8000000, 4000000, 2000000, 1000000].map((n) => (
              <option key={n} value={n}>
                {n / 1000000} Mbps
              </option>
            ))}
          </select>
        </label>
        {(item.value?.MediaSources?.length ?? 0) > 1 && (
          <label>
            Version
            <select
              value={mediaVersion || source?.Id || ''}
              onChange={(e) => {
                changing();
                setMediaVersion(e.target.value);
              }}
            >
              {item.value!.MediaSources!.map((s) => (
                <option key={s.Id} value={s.Id}>
                  {s.Name ?? s.Container ?? s.Id}
                </option>
              ))}
            </select>
          </label>
        )}
        {(item.value?.Chapters?.length ?? 0) > 0 && (
          <label>
            Chapters
            <select value="" onChange={(e) => seek(Number(e.target.value) / 10000000)}>
              <option value="">Choose chapter…</option>
              {item.value!.Chapters!.map((c, i) => (
                <option key={c.StartPositionTicks} value={c.StartPositionTicks}>
                  {c.Name ?? 'Chapter ' + (i + 1)}
                </option>
              ))}
            </select>
          </label>
        )}
        <label>
          Audio
          <select
            value={audio ?? source?.DefaultAudioStreamIndex ?? ''}
            onChange={(e) => {
              changing();
              setAudio(Number(e.target.value));
              setTranscode(true);
            }}
          >
            {source?.MediaStreams.filter((s) => s.Type === 'Audio').map((s) => (
              <option key={s.Index} value={s.Index}>
                {s.DisplayTitle ?? s.Language ?? s.Codec ?? 'Audio ' + s.Index}
              </option>
            ))}
          </select>
        </label>
        <label>
          Subtitles
          <select
            value={subtitle}
            onChange={(e) => {
              changing();
              setSubtitle(Number(e.target.value));
            }}
          >
            <option value={-1}>Off</option>
            {source?.MediaStreams.filter((s) => s.Type === 'Subtitle').map((s) => (
              <option key={s.Index} value={s.Index}>
                {s.DisplayTitle ?? s.Language ?? 'Subtitle ' + s.Index}
              </option>
            ))}
          </select>
        </label>
        <button
          onClick={() => {
            changing();
            setTranscode(true);
            setAttempt((a) => a + 1);
          }}
        >
          Try compatible stream
        </button>
        <a href={api.legacy()} target="_blank" rel="noopener noreferrer">
          Open existing client ↗
        </a>
      </div>
      <p className="muted">
        Playback quality and available tracks depend on your server, media, and browser. Live
        channels are subject to tuner availability.
      </p>
    </main>
  );
}
