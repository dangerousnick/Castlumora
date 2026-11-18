import { expect, it } from 'vitest';
import { playbackDefaults } from './profile';
import type { MediaSource } from '../api/types';
const source: MediaSource = {
  Id: 'video',
  MediaStreams: [
    { Index: 0, Type: 'Video' },
    { Index: 1, Type: 'Audio', IsDefault: true, Language: 'eng' },
    { Index: 2, Type: 'Audio', Language: 'spa' },
  ],
  DefaultAudioStreamIndex: 1,
};
it('keeps native default playback compatible with direct play', () => {
  expect(playbackDefaults(source)).toEqual({ audio: 1, subtitle: -1, requiresStreaming: false });
});
it('routes an alternate preferred audio track through the server', () => {
  expect(playbackDefaults({ ...source, DefaultAudioStreamIndex: 2 })).toEqual({
    audio: 2,
    subtitle: -1,
    requiresStreaming: true,
  });
});
it('honors preferred subtitles and explicit off without confusing the two', () => {
  const subtitles = { ...source, DefaultSubtitleStreamIndex: 3 };
  expect(playbackDefaults(subtitles).requiresStreaming).toBe(true);
  expect(playbackDefaults(subtitles, undefined, -1)).toEqual({
    audio: 1,
    subtitle: -1,
    requiresStreaming: false,
  });
});
it('honors explicit audio selection and handles silent videos', () => {
  expect(playbackDefaults(source, 2).requiresStreaming).toBe(true);
  expect(playbackDefaults({ Id: 'silent', MediaStreams: [] })).toEqual({
    audio: undefined,
    subtitle: -1,
    requiresStreaming: false,
  });
});
