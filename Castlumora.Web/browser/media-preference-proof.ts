import { expect } from '@playwright/test';
import type { Page } from '@playwright/test';

type ProofWindow = Window & {
  castlumoraAudioProof?: { context: AudioContext; analyser: AnalyserNode };
};

// Measurement only: never changes the video's track, playback state or source.
export async function verifyPreferredFixtureTracks(page: Page) {
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.readyState), {
      timeout: 20000,
    })
    .toBeGreaterThanOrEqual(2);
  const position = await page
    .locator('video')
    .evaluate((video: HTMLVideoElement) => video.currentTime);
  await expect
    .poll(() => page.locator('video').evaluate((video: HTMLVideoElement) => video.currentTime), {
      timeout: 20000,
    })
    .toBeGreaterThan(position + 0.2);
  await expect
    .poll(
      () =>
        page.locator('video').evaluate((video: HTMLVideoElement) =>
          Array.from(video.textTracks)
            .flatMap((track) =>
              Array.from(track.activeCues ?? []).map((cue) => (cue as VTTCue).text),
            )
            .join(' '),
        ),
      { timeout: 10000 },
    )
    .toContain('Castlumora profile subtitle fixture');
  await page.evaluate(() => {
    const button = document.createElement('button');
    button.id = 'measure-owned-fixture-audio';
    button.textContent = 'Measure owned fixture audio';
    button.onclick = () => {
      const context = new AudioContext();
      const analyser = context.createAnalyser();
      analyser.fftSize = 8192;
      context.createMediaElementSource(document.querySelector('video')!).connect(analyser);
      analyser.connect(context.destination);
      (window as ProofWindow).castlumoraAudioProof = { context, analyser };
      void context.resume();
    };
    document.body.appendChild(button);
  });
  await page.locator('#measure-owned-fixture-audio').click();
  let measured = 0;
  try {
    await expect
      .poll(
        async () => {
          measured = await page.evaluate(() => {
            const proof = (window as ProofWindow).castlumoraAudioProof;
            if (!proof) return 0;
            const data = new Uint8Array(proof.analyser.frequencyBinCount);
            proof.analyser.getByteFrequencyData(data);
            let peak = 0;
            for (let index = 1; index < data.length; index++) {
              const frequency = (index * proof.context.sampleRate) / proof.analyser.fftSize;
              if (frequency > 100 && frequency < 2000 && data[index] > data[peak]) peak = index;
            }
            return data[peak] > 20 ? (peak * proof.context.sampleRate) / proof.analyser.fftSize : 0;
          });
          return Math.abs(measured - 880);
        },
        { timeout: 10000 },
      )
      .toBeLessThan(40);
    return measured;
  } finally {
    await page.evaluate(async () => {
      await (window as ProofWindow).castlumoraAudioProof?.context.close();
      delete (window as ProofWindow).castlumoraAudioProof;
      document.querySelector('#measure-owned-fixture-audio')?.remove();
    });
  }
}
