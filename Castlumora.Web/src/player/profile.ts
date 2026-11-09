import type { Api } from "../api/client";
export function browserProfile() {
    const video = document.createElement("video");
    const supportsAvc = !!video.canPlayType('video/mp4; codecs="avc1.42E01E, mp4a.40.2"');
    return {
        Name: "Castlumora Browser",
        MaxStreamingBitrate: 20000000,
        MaxStaticBitrate: 100000000,
        DirectPlayProfiles: supportsAvc ? [{ Container: "mp4,m4v", Type: "Video", VideoCodec: "h264", AudioCodec: "aac,mp3" }] : [],
        TranscodingProfiles: [{ Container: "ts", Type: "Video", VideoCodec: "h264", AudioCodec: "aac", Protocol: "hls", Context: "Streaming", MaxAudioChannels: "2", MinSegments: 2, SegmentLength: 6 }],
        CodecProfiles: [{ Type: "Video", Codec: "h264", Conditions: [{ Condition: "LessThanEqual", Property: "VideoBitDepth", Value: "8", IsRequired: true }] }],
        SubtitleProfiles: [
            { Format: "vtt", Method: "External" },
            { Format: "srt", Method: "Encode" },
            { Format: "ass", Method: "Encode" },
            { Format: "ssa", Method: "Encode" },
            { Format: "pgssub", Method: "Encode" },
            { Format: "dvdsub", Method: "Encode" },
        ],
    };
}
export function authenticatedMediaUrl(api: Api, path: string) {
    const url = new URL(path, api.server + "/");
    const base = new URL(api.server + "/");
    if (url.origin !== base.origin || !url.pathname.startsWith(base.pathname)) throw new Error("Server returned a media URL outside the paired server.");
    url.searchParams.set("api_key", api.session!.token);
    return url.href;
}
export function ticks(seconds: number) {
    return Math.max(0, Math.round(seconds * 10000000));
}
