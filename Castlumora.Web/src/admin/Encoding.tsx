import { useMemo } from 'react';
import { useApp, useLoad } from '../state';
import { Feedback } from '../components/Media';
import { adminApi } from './api';
import { SettingsEditor } from './Controls';
export default function Encoding() {
  const { api } = useApp();
  const admin = useMemo(() => adminApi(api), [api]);
  const config = useLoad(() => admin.config('encoding'), [admin]);
  return (
    <>
      <h1>Playback & encoding</h1>
      <p>
        Use your server’s supported encoders and hardware. Changes are validated and persisted by
        the existing backend.
      </p>
      <Feedback busy={config.busy} error={config.error} />
      {config.value && (
        <SettingsEditor
          title="Encoding"
          value={config.value}
          fields={[
            { key: 'EncoderAppPath', label: 'FFmpeg executable path' },
            {
              key: 'HardwareAccelerationType',
              label: 'Hardware acceleration',
              options: ['none', 'amf', 'qsv', 'nvenc', 'v4l2m2m', 'vaapi', 'videotoolbox', 'rkmpp'],
            },
            { key: 'EnableHardwareEncoding', label: 'Hardware encoding', type: 'boolean' },
            {
              key: 'EncodingThreadCount',
              label: 'Encoding threads (-1 = automatic)',
              type: 'number',
            },
            { key: 'TranscodingTempPath', label: 'Transcoding temporary folder' },
            { key: 'EnableThrottling', label: 'Throttle completed transcodes', type: 'boolean' },
            { key: 'H264Crf', label: 'H264 quality (CRF)', type: 'number' },
            { key: 'H265Crf', label: 'H265 quality (CRF)', type: 'number' },
            { key: 'AllowHevcEncoding', label: 'Allow HEVC encoding', type: 'boolean' },
            { key: 'AllowAv1Encoding', label: 'Allow AV1 encoding', type: 'boolean' },
            { key: 'EnableTonemapping', label: 'Tone mapping', type: 'boolean' },
            { key: 'VaapiDevice', label: 'VAAPI device' },
            { key: 'QsvDevice', label: 'QSV device' },
          ]}
          save={(value) => admin.saveConfig(value, 'encoding')}
        />
      )}
    </>
  );
}
