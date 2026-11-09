import type { Api } from '../api/client';
export type ObjectData = Record<string, unknown>;
export interface LibraryInfo {
  Name: string;
  ItemId: string;
  Locations: string[];
  CollectionType: string;
  LibraryOptions: ObjectData;
}
export interface ServerTask {
  Id: string;
  Name: string;
  Description: string;
  State: string;
  CurrentProgressPercentage?: number;
  Triggers: ObjectData[];
}
export interface PluginInfo {
  Id: string;
  Name: string;
  Version: string;
  Status: string;
  Description?: string;
}
export function adminApi(api: Api) {
  return {
    config: (key?: string) =>
      api.request<ObjectData>('System/Configuration' + (key ? '/' + key : '')),
    saveConfig: (value: ObjectData, key?: string) =>
      api.request<void>('System/Configuration' + (key ? '/' + key : ''), {
        method: 'POST',
        body: JSON.stringify(value),
      }),
    libraries: () => api.request<LibraryInfo[]>('Library/VirtualFolders'),
    createLibrary: (name: string, type: string, path: string) =>
      api.request<void>(
        'Library/VirtualFolders',
        {
          method: 'POST',
          body: JSON.stringify({
            LibraryOptions: { PathInfos: [{ Path: path }], EnableRealtimeMonitor: true },
          }),
        },
        { name, collectionType: type, refreshLibrary: true },
      ),
    saveLibrary: (id: string, options: ObjectData) =>
      api.request<void>('Library/VirtualFolders/LibraryOptions', {
        method: 'POST',
        body: JSON.stringify({ Id: id, LibraryOptions: options }),
      }),
    scan: () => api.request<void>('Library/Refresh', { method: 'POST' }),
    createUser: (name: string) =>
      api.request<{ Id: string; Name: string }>('Users/New', {
        method: 'POST',
        body: JSON.stringify({ Name: name }),
      }),
    savePolicy: (id: string, policy: ObjectData) =>
      api.request<void>(`Users/${id}/Policy`, { method: 'POST', body: JSON.stringify(policy) }),
    setPassword: (id: string, password: string, currentPassword?: string) =>
      api.request<void>(`Users/${id}/Password`, {
        method: 'POST',
        body: JSON.stringify({ NewPw: password, CurrentPw: currentPassword }),
      }),
    tasks: () => api.request<ServerTask[]>('ScheduledTasks'),
    runTask: (id: string) => api.request<void>(`ScheduledTasks/Running/${id}`, { method: 'POST' }),
    stopTask: (id: string) =>
      api.request<void>(`ScheduledTasks/Running/${id}`, { method: 'DELETE' }),
    triggers: (id: string, triggers: ObjectData[]) =>
      api.request<void>(`ScheduledTasks/${id}/Triggers`, {
        method: 'POST',
        body: JSON.stringify(triggers),
      }),
    plugins: () => api.request<PluginInfo[]>('Plugins'),
    pluginState: (id: string, version: string, enable: boolean) =>
      api.request<void>(
        `Plugins/${id}/${encodeURIComponent(version)}/${enable ? 'Enable' : 'Disable'}`,
        { method: 'POST' },
      ),
    removePlugin: (id: string, version: string) =>
      api.request<void>(`Plugins/${id}/${encodeURIComponent(version)}`, { method: 'DELETE' }),
    installPlugin: (name: string, guid: string, version?: string) =>
      api.request<void>(
        `Packages/Installed/${encodeURIComponent(name)}`,
        { method: 'POST' },
        { assemblyGuid: guid, version },
      ),
    tuner: (value: ObjectData) =>
      api.request<ObjectData>('LiveTv/TunerHosts', { method: 'POST', body: JSON.stringify(value) }),
    listing: (value: ObjectData) =>
      api.request<ObjectData>(
        'LiveTv/ListingProviders',
        { method: 'POST', body: JSON.stringify(value) },
        { validateListings: true },
      ),
  };
}
