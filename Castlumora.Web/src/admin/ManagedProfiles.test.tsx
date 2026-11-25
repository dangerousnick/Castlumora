// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Api } from '../api/client';
import { AppContext, type AppState } from '../state';
import ManagedProfiles from './ManagedProfiles';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

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

it('edits only the selected profile display name and refreshes after saving', async () => {
  const api = new Api('https://example.test');
  let name = 'Kids';
  const request = vi.spyOn(api, 'request').mockImplementation(async (path, options) => {
    if (path === 'Users' || path === 'Library/VirtualFolders') return [];
    if (path === 'Castlumora/Profiles/Managed/child/Name') {
      name = JSON.parse(options!.body as string).name;
      return undefined;
    }
    return {
      profiles: [
        { id: 'child', name, accountName: 'Account', preset: 'kids', ready: true, exists: true },
      ],
    };
  });
  const onChanged = vi.fn();
  render(
    <AppContext value={{ api } as unknown as AppState}>
      <ManagedProfiles onChanged={onChanged} />
    </AppContext>,
  );
  fireEvent.click(await screen.findByRole('button', { name: 'Rename Kids' }));
  fireEvent.change(screen.getByLabelText('Profile display name'), { target: { value: 'Family' } });
  fireEvent.click(screen.getByRole('button', { name: 'Save profile name' }));
  await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
  expect(request).toHaveBeenCalledWith('Castlumora/Profiles/Managed/child/Name', {
    method: 'PUT',
    body: JSON.stringify({ name: 'Family' }),
  });
  await screen.findByRole('button', { name: 'Rename Family' });
  expect(screen.queryByLabelText('Profile display name')).toBeNull();
});

it.each([
  ['provisioning', 'Provisioning interrupted — unavailable; preserve for recovery or remove'],
  ['deleting', 'Deletion pending — unavailable; retry removal'],
  ['review', 'Legacy pending profile — unavailable; administrator review required'],
])('explains %s without exposing an unsafe reactivation action', async (lifecycle, label) => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path === 'Users' || path === 'Library/VirtualFolders') return [];
    return {
      profiles: [
        {
          id: 'child',
          name: 'Kids',
          accountName: 'Account',
          preset: 'kids',
          ready: false,
          exists: true,
          lifecycle,
        },
      ],
    };
  });
  render(
    <AppContext value={{ api } as unknown as AppState}>
      <ManagedProfiles onChanged={() => {}} />
    </AppContext>,
  );
  await screen.findByText(label);
  expect(screen.queryByRole('button', { name: /recover|reactivate/i })).toBeNull();
  expect(screen.getByRole('button', { name: 'Delete Kids' })).toBeTruthy();
});

it('confirms recovery of explicit provisioning and refreshes only after server success', async () => {
  const api = new Api('https://example.test');
  let ready = false;
  const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path === 'Users' || path === 'Library/VirtualFolders') return [];
    if (path === 'Castlumora/Profiles/Managed/child/Recover') {
      ready = true;
      return undefined;
    }
    return {
      recoveryAvailable: true,
      profiles: [
        {
          id: 'child',
          name: 'Kids',
          accountName: 'Account',
          preset: 'kids',
          ready,
          exists: true,
          lifecycle: ready ? 'active' : 'provisioning',
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
    fireEvent.click(await screen.findByRole('button', { name: 'Recover Kids' }));
    expect(ready).toBe(false);
    confirm.mockReturnValue(true);
    fireEvent.click(screen.getByRole('button', { name: 'Recover Kids' }));
    await waitFor(() => expect(onChanged).toHaveBeenCalledOnce());
    expect(request).toHaveBeenCalledWith('Castlumora/Profiles/Managed/child/Recover', {
      method: 'POST',
      body: '{}',
    });
    await screen.findByText('Available', { exact: true });
    expect(screen.queryByRole('button', { name: 'Recover Kids' })).toBeNull();
  } finally {
    confirm.mockRestore();
  }
});

it.each([
  ['deleting', false, true],
  ['review', false, true],
  ['active', true, true],
  ['provisioning', false, false],
])(
  'never offers recovery for lifecycle=%s ready=%s exists=%s',
  async (lifecycle, ready, exists) => {
    const api = new Api('https://example.test');
    vi.spyOn(api, 'request').mockImplementation(async (path) =>
      path === 'Users' || path === 'Library/VirtualFolders'
        ? []
        : {
            recoveryAvailable: true,
            profiles: [
              {
                id: 'child',
                name: 'Kids',
                accountName: 'Account',
                preset: 'kids',
                ready,
                exists,
                lifecycle,
              },
            ],
          },
    );
    render(
      <AppContext value={{ api } as unknown as AppState}>
        <ManagedProfiles onChanged={() => {}} />
      </AppContext>,
    );
    await screen.findByRole('button', { name: 'Rename Kids' });
    expect(screen.queryByRole('button', { name: 'Recover Kids' })).toBeNull();
  },
);

it('keeps failed recovery unavailable and reports the server error without a success refresh', async () => {
  const api = new Api('https://example.test');
  vi.spyOn(api, 'request').mockImplementation(async (path) => {
    if (path === 'Users' || path === 'Library/VirtualFolders') return [];
    if (path.endsWith('/Recover')) throw new Error('Private key requires administrator review');
    return {
      recoveryAvailable: true,
      profiles: [
        {
          id: 'child',
          name: 'Kids',
          accountName: 'Account',
          preset: 'kids',
          ready: false,
          exists: true,
          lifecycle: 'provisioning',
        },
      ],
    };
  });
  const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
  const onChanged = vi.fn();
  try {
    render(
      <AppContext value={{ api } as unknown as AppState}>
        <ManagedProfiles onChanged={onChanged} />
      </AppContext>,
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Recover Kids' }));
    await screen.findByText(/Private key requires administrator review/);
    expect(onChanged).not.toHaveBeenCalled();
    expect(screen.queryByText('Available', { exact: true })).toBeNull();
    expect(screen.getByRole('button', { name: 'Recover Kids' })).toBeTruthy();
  } finally {
    confirm.mockRestore();
  }
});

it.each([false, true])(
  'confirms scoped access editing with a reviewed revision and handles denial (%s)',
  async (denied) => {
    const api = new Api('https://example.test');
    const first = '123456781234123412341234567890ab';
    const formatted = '12345678-1234-1234-1234-1234567890ab';
    const revision = 'a'.repeat(64);
    const onChanged = vi.fn();
    const request = vi.spyOn(api, 'request').mockImplementation(async (path) => {
      if (path === 'Users') return [];
      if (path === 'Library/VirtualFolders')
        return [
          { ItemId: first, Name: 'Original library' },
          { ItemId: 'second', Name: 'New library' },
        ];
      if (path.endsWith('/Access')) {
        if (denied) throw new Error('Access changed. Cancel access edit and reopen it.');
        return undefined;
      }
      return {
        accessEditingAvailable: true,
        profiles: [
          {
            id: 'child',
            name: 'Kids',
            accountName: 'Owner',
            preset: 'kids',
            lifecycle: 'active',
            ready: true,
            exists: true,
            libraries: [formatted],
            accessRevision: revision,
          },
        ],
      };
    });
    render(
      <AppContext value={{ api } as unknown as AppState}>
        <ManagedProfiles onChanged={onChanged} />
      </AppContext>,
    );
    fireEvent.click(await screen.findByRole('button', { name: 'Edit access for Kids' }));
    const form = within(screen.getByRole('form', { name: 'Access settings for Kids' }));
    const original = form.getByRole('checkbox', { name: 'Original library' }) as HTMLInputElement;
    expect(original.checked).toBe(true);
    fireEvent.click(original);
    const apply = form.getByRole('button', { name: 'Apply profile access' }) as HTMLButtonElement;
    expect(apply.disabled).toBe(true);
    fireEvent.click(form.getByRole('checkbox', { name: 'New library' }));
    fireEvent.change(form.getByLabelText('Updated profile preset'), { target: { value: 'adult' } });
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    fireEvent.click(apply);
    expect(request.mock.calls.some(([path]) => path.endsWith('/Access'))).toBe(false);
    confirm.mockReturnValue(true);
    fireEvent.click(apply);
    await waitFor(() =>
      expect(request).toHaveBeenCalledWith('Castlumora/Profiles/Managed/child/Access', {
        method: 'PUT',
        body: JSON.stringify({ preset: 'adult', libraries: ['second'], revision }),
      }),
    );
    if (denied) {
      await screen.findByText('Access changed. Cancel access edit and reopen it.');
      expect(onChanged).not.toHaveBeenCalled();
      expect(
        screen.queryByText(
          'Profile access updated. Devices signed out; identity and viewing history retained.',
        ),
      ).toBeNull();
      expect(form.getByLabelText('Updated profile preset')).toHaveProperty('value', 'adult');
      fireEvent.click(form.getByRole('button', { name: 'Cancel access edit' }));
    } else {
      await screen.findByText(
        'Profile access updated. Devices signed out; identity and viewing history retained.',
      );
      expect(onChanged).toHaveBeenCalledOnce();
    }
    await waitFor(() =>
      expect(screen.queryByRole('form', { name: 'Access settings for Kids' })).toBeNull(),
    );
  },
);
