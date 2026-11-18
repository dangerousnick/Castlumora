import type { Item } from '../api/types';
import { useApp } from '../state';
import { ActionStatus, useAction } from '../admin/Controls';
import { liveApi } from './api';
export function RecordingControls({ item, changed }: { item: Item; changed: () => void }) {
  const { api, session } = useApp();
  const action = useAction();
  const manage =
    session.user.Policy?.IsAdministrator || session.user.Policy?.EnableLiveTvManagement;
  if (!manage) return null;
  return (
    <div className="recording-controls">
      <button
        disabled={action.busy}
        onClick={() => {
          if (
            window.confirm(
              `Remove “${item.Name}” from the library? Its media file is retained and may reappear after a scan.`,
            )
          )
            void action.run(async () => {
              await liveApi(api, session.user.Id).removeRecording(item.Id);
              changed();
            }, 'Removed from library; media file retained.');
        }}
      >
        Remove from library
      </button>
      {item.CanDelete === true && (
        <button
          disabled={action.busy}
          onClick={() => {
            if (
              window.confirm(
                `Permanently delete recording “${item.Name}” and its media file? This cannot be undone.`,
              )
            )
              void action.run(async () => {
                await liveApi(api, session.user.Id).deleteRecordingFile(item.Id);
                changed();
              }, 'Recording and media file deleted.');
          }}
        >
          Delete recording file
        </button>
      )}
      <ActionStatus message={action.message} />
    </div>
  );
}
