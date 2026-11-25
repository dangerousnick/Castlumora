import type { Api } from '../api/client';
import { ApiError } from '../api/client';
import type { ObjectData } from '../admin/api';
export interface RegistrationSettings {
  [key: string]: unknown;
  revision: number;
  enabled: boolean;
  requireInvitation: boolean;
  defaultPolicy: ObjectData;
}
export interface RegistrationState {
  settings: RegistrationSettings;
  invitations: {
    id: string;
    name: string;
    expiresAt: string;
    maxUses: number;
    successfulUses: number;
    reservedUses: number;
    revoked: boolean;
  }[];
  registrations: {
    id: string;
    username: string;
    invitationId?: string;
    userId?: string;
    at: string;
    status: string;
  }[];
}
export function registrationApi(api: Api) {
  return {
    availability: () =>
      api.request<{ enabled: boolean; requireInvitation: boolean }>('Castlumora/Registration'),
    register: async (username: string, password: string, invitation?: string) => {
      try {
        await api.request<void>('Castlumora/Registration', {
          method: 'POST',
          body: JSON.stringify({ username, password, invitation: invitation || undefined }),
        });
      } catch (error) {
        if (error instanceof ApiError && error.status === 429)
          throw new Error('Too many signup attempts. Please wait a minute before trying again.');
        if (error instanceof ApiError && error.status === 400)
          throw new Error(
            'Signup could not be completed. Check the invitation and username, or ask the administrator. If an earlier attempt was interrupted, try signing in.',
          );
        throw error;
      }
    },
    admin: () => api.request<RegistrationState>('Castlumora/Registration/Admin'),
    saveSettings: (settings: RegistrationSettings) =>
      api.request<RegistrationSettings>('Castlumora/Registration/Settings', {
        method: 'PUT',
        body: JSON.stringify(settings),
      }),
    createInvitation: (value: {
      name: string;
      expiresAt: string;
      maxUses: number;
      policy?: ObjectData;
    }) =>
      api.request<{ id: string; token: string }>('Castlumora/Registration/Invitations', {
        method: 'POST',
        body: JSON.stringify(value),
      }),
    revoke: (id: string) =>
      api.request<void>(`Castlumora/Registration/Invitations/${id}`, { method: 'DELETE' }),
    resolve: (id: string, consume: boolean) =>
      api.request<void>(
        `Castlumora/Registration/Reservations/${id}/Resolve`,
        { method: 'POST' },
        { consume },
      ),
  };
}
