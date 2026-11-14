# Generated importer test clip

`import-owned.mp4` is a one-second, 64×64 purple frame with a generated 440Hz tone.
No third-party footage, music or external source material is used. Distributed
under the repository's GPL license as a test fixture. It is not representative of
real-world media, hardware acceleration or detection accuracy.

Recreate with an installed FFmpeg supporting libx264/AAC (encoder versions can
change the resulting bytes):

```bash
ffmpeg -hide_banner -loglevel error \
  -f lavfi -i color=c=purple:s=64x64:r=10:d=1 \
  -f lavfi -i sine=frequency=440:duration=1 \
  -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \
  -movflags +faststart import-owned.mp4
```

Tests inspect its real container, mutate targeted structural fields to test
rejections, and run native FFprobe under the import-only protocol/format limits.
The queue/runner test serves it through an owned TLS fixture and the validating
tunnel. No public media is downloaded for these integration tests.
