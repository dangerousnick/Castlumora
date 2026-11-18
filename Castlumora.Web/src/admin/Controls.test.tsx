// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { SettingsEditor } from './Controls';
afterEach(cleanup);
it('edits a visible setting while retaining unknown server configuration', async () => {
  const save = vi.fn().mockResolvedValue(undefined);
  render(
    <SettingsEditor
      title="Encoding"
      value={{ H264Crf: 23, Future: { untouched: true } }}
      fields={[{ key: 'H264Crf', label: 'Quality', type: 'number' }]}
      save={save}
    />,
  );
  fireEvent.change(screen.getByLabelText('Quality'), { target: { value: '24' } });
  fireEvent.click(screen.getByRole('button', { name: /^Save encoding$/ }));
  await waitFor(() =>
    expect(save).toHaveBeenCalledWith({ H264Crf: 24, Future: { untouched: true } }),
  );
});
it('rejects non-object advanced settings before contacting the server', async () => {
  const save = vi.fn();
  render(<SettingsEditor title="Config" value={{ enabled: true }} save={save} />);
  fireEvent.change(screen.getByLabelText('Config advanced settings'), { target: { value: '[]' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save advanced settings' }));
  await waitFor(() => expect(screen.getByRole('status').textContent).toContain('JSON object'));
  expect(save).not.toHaveBeenCalled();
});
