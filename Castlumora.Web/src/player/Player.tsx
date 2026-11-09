import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft } from "lucide-react";
import Hls from "hls.js";
import type { MediaSource } from "../api/types";
import { errorText, useApp, useLoad } from "../state";
import { Feedback } from "../components/Media";
import { authenticatedMediaUrl, browserProfile, ticks } from "./profile";
export default function Player() {
    const { id = "" } = useParams();
    const { api } = useApp();
    const videoRef = useRef<HTMLVideoElement>(null);
    const position = useRef(0);
    const [audio, setAudio] = useState<number>();
    const [subtitle, setSubtitle] = useState(-1);
    const [transcode, setTranscode] = useState(false);
    const [attempt, setAttempt] = useState(0);
    const [source, setSource] = useState<MediaSource>();
    const [error, setError] = useState("");
    const [reportError, setReportError] = useState("");
    const [loading, setLoading] = useState(true);
    const item = useLoad(() => api.item(id), [api, id]);
    const initial = item.value?.UserData?.PlaybackPositionTicks ?? 0;
    useEffect(() => {
        if (!item.value) return;
        const video = videoRef.current!;
        let cancelled = false;
        let hls: Hls | undefined;
        let playSession = "";
        let liveStream: string | undefined;
        let mediaSource = "";
        let started = false;
        let offset = 0;
        let direct = true;
        let timer: ReturnType<typeof setInterval> | undefined;
        setLoading(true);
        setError("");
        const start = position.current || initial;
        position.current = start;
        function body() {
            return { ItemId: id, MediaSourceId: mediaSource, PlaySessionId: playSession, PositionTicks: position.current, IsPaused: video.paused, IsMuted: video.muted, CanSeek: item.value?.Type !== "TvChannel", PlayMethod: direct ? "DirectPlay" : "Transcode", AudioStreamIndex: audio, SubtitleStreamIndex: subtitle, LiveStreamId: liveStream };
        }
        function updatePosition() {
            if (video.readyState > 0) position.current = ticks(video.currentTime) + offset;
        }
        async function report(kind: "Playing" | "Playing/Progress" | "Playing/Stopped") {
            try {
                await api.report(kind, body());
                setReportError("");
            } catch {
                setReportError("Playback progress could not be saved. Check your connection.");
            }
        }
        function playing() {
            setLoading(false);
            if (!started) {
                started = true;
                void report("Playing");
            } else void report("Playing/Progress");
        }
        function changed() {
            updatePosition();
            if (started) void report("Playing/Progress");
        }
        function failed() {
            setLoading(false);
            setError("This stream could not be played. Try the compatible stream, or use the existing client.");
        }
        video.addEventListener("playing", playing);
        video.addEventListener("pause", changed);
        video.addEventListener("seeked", changed);
        video.addEventListener("timeupdate", updatePosition);
        video.addEventListener("error", failed);
        void (async () => {
            try {
                const info = await api.playback(id, browserProfile(), start, audio, subtitle, transcode || audio !== undefined || subtitle >= 0);
                playSession = info.PlaySessionId;
                if (info.ErrorCode || !info.MediaSources.length) throw new Error(info.ErrorCode ?? "No playable media source was returned.");
                const selected = info.MediaSources[0];
                mediaSource = selected.Id;
                liveStream = selected.LiveStreamId;
                if (cancelled) {
                    await api.cleanup(playSession, liveStream);
                    return;
                }
                setSource(selected);
                direct = !!selected.SupportsDirectPlay && !transcode && audio === undefined && subtitle < 0;
                let url: string;
                if (direct) {
                    url = authenticatedMediaUrl(api, api.url(`Videos/${id}/stream`, { Static: true, MediaSourceId: mediaSource, DeviceId: api.session!.deviceId, PlaySessionId: playSession }).href);
                } else {
                    if (!selected.TranscodingUrl) throw new Error("The server did not return a compatible streaming URL.");
                    url = authenticatedMediaUrl(api, selected.TranscodingUrl);
                    offset = start;
                }
                if (/\.m3u8(?:\?|$)/i.test(url) && !video.canPlayType("application/vnd.apple.mpegurl")) {
                    if (!Hls.isSupported()) throw new Error("HLS is not supported in this browser.");
                    hls = new Hls({
                        xhrSetup(xhr, url) {
                            const target = new URL(url, api.server + "/");
                            if (target.origin === new URL(api.server).origin) xhr.setRequestHeader("X-Emby-Token", api.session!.token);
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
                    const stream = selected.MediaStreams.find((s) => s.Type === "Subtitle" && s.Index === subtitle);
                    if (stream?.DeliveryUrl && stream.IsExternal) {
                        const track = document.createElement("track");
                        track.kind = "subtitles";
                        track.label = stream.DisplayTitle ?? stream.Language ?? "Subtitles";
                        track.srclang = stream.Language ?? "en";
                        track.src = authenticatedMediaUrl(api, stream.DeliveryUrl);
                        track.default = true;
                        video.appendChild(track);
                    }
                }
                // Playback requires a user gesture on browsers that reject autoplay.
                void video.play().catch(() => setLoading(false));
                timer = setInterval(() => {
                    if (started && !video.paused) {
                        updatePosition();
                        void report("Playing/Progress");
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
            if (started) void api.report("Playing/Stopped", body()).catch(() => {});
            video.pause();
            video.removeEventListener("playing", playing);
            video.removeEventListener("pause", changed);
            video.removeEventListener("seeked", changed);
            video.removeEventListener("timeupdate", updatePosition);
            video.removeEventListener("error", failed);
            video.onloadedmetadata = null;
            hls?.destroy();
            video.removeAttribute("src");
            video.querySelectorAll("track").forEach((t) => t.remove());
            video.load();
            if (playSession) void api.cleanup(playSession, liveStream);
        };
    }, [api, id, initial, item.value, audio, subtitle, transcode, attempt]);
    return (
        <main className="player-page">
            <div className="player-heading">
                <Link to={item.value?.Type === "TvChannel" ? "/live" : "/item/" + id} className="back-link">
                    <ArrowLeft size={20} />
                    Back
                </Link>
                <h1>{item.value?.Name ?? "Your screening room"}</h1>
            </div>
            <Feedback busy={item.busy} error={item.error} />
            <video ref={videoRef} controls playsInline crossOrigin="anonymous" aria-label={item.value?.Name ?? "Video player"} />
            <Feedback busy={loading && !error} error={error} />
            {reportError && (
                <p role="status" className="notice">
                    {reportError}
                </p>
            )}
            <div className="player-tools">
                <label>
                    Audio<select
                        value={audio ?? source?.DefaultAudioStreamIndex ?? ""}
                        onChange={(e) => {
                            setAudio(Number(e.target.value));
                            setTranscode(true);
                        }}
                    >
                        {source?.MediaStreams.filter((s) => s.Type === "Audio").map((s) => (
                            <option key={s.Index} value={s.Index}>
                                {s.DisplayTitle ?? s.Language ?? s.Codec ?? "Audio " + s.Index}
                            </option>
                        ))}
                    </select>
                </label>
                <label>
                    Subtitles<select value={subtitle} onChange={(e) => setSubtitle(Number(e.target.value))}>
                        <option value={-1}>Off</option>
                        {source?.MediaStreams.filter((s) => s.Type === "Subtitle").map((s) => (
                            <option key={s.Index} value={s.Index}>
                                {s.DisplayTitle ?? s.Language ?? "Subtitle " + s.Index}
                            </option>
                        ))}
                    </select>
                </label>
                <button
                    onClick={() => {
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
            <p className="muted">Playback quality and available tracks depend on your server, media, and browser. Live channels are subject to tuner availability.</p>
        </main>
    );
}
