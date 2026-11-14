// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { AppContext, type AppState } from '../state';
import ManagedProfiles from './ManagedProfiles';

afterEach(cleanup);

it('requires explicit libraries and submits the chosen owning account and kids preset', async () => {
  const api = new Api('https://example.test');
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path === 'Users') return [{ Id: 'owner', Name: 'Account' }];
    if (path === 'Library/VirtualFolders') return [{ ItemId: 'library', Name: 'Family library' }];
    if (path === 'Castlumora/Profiles/Managed') return { profiles: [] };
    return { id: 'profile', ready: true };
  });
  const onChanged = vi.fn();
  render(
    <AppContext value={{ api } as unknown as AppState}>
      <ManagedProfiles onChanged={onChanged} />
    </AppContext>,
  );
  fireEvent.change(await screen.findByRole('combobox', { name: 'Owning account' }), {
    target: { value: 'owner' },
  });
  fireEvent.change(screen.getByLabelText('Profile name'), { target: { value: 'Kids' } });
  const submit = screen.getByRole('button', { name: 'Create viewing profile' });
  expect((submit as HTMLButtonElement).disabled).toBe(true);
  fireEvent.click(await screen.findByRole('checkbox', { name: 'Family library' }));
  fireEvent.click(submit);
  await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
  expect(request).toHaveBeenCalledWith('Castlumora/Profiles', {
    method: 'POST',
    body: JSON.stringify({
      accountId: 'owner',
      name: 'Kids',
      preset: 'kids',
      libraries: ['library'],
    }),
  });
  expect((screen.getByLabelText('Profile name') as HTMLInputElement).value).toBe('');
});

it('requires confirmation and removes only the selected managed profile', async () => {
  const api = new Api('https://example.test');
  let removed = false;
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path === 'Users' || path === 'Library/VirtualFolders') return [];
    if (path === 'Castlumora/Profiles/Managed/child') {
      removed = true;
      return undefined;
    }
    return {
      profiles: removed
        ? []
        : [
            {
              id: 'child',
              name: 'Kids',
              accountName: 'Account',
              preset: 'kids',
              ready: true,
              exists: true,
            },
          ],
    };
  });
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
  const onChanged = vi.fn();
  try {
    render(
      <AppContext value={{ api } as unknown as AppState}>
        <ManagedProfiles onChanged={onChanged} />
      </AppContext>,
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Delete Kids' }));
    expect(removed).toBe(false);
    confirm.mockReturnValue(true);
    fireEvent.click(screen.getByRole('button', { name: 'Delete Kids' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(request).toHaveBeenCalledWith('Castlumora/Profiles/Managed/child', { method: 'DELETE' });
    await screen.findByText('No managed profiles.');
  } finally {
    confirm.mockRestore();
  }
});
