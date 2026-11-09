import { useState } from 'react';
import type { Item } from '../api/types';
import { useApp, useLoad } from '../state';
import { Feedback } from './Media';
export function MediaPicker({
  label,
  onChoose,
  types = 'Movie,Series',
}: {
  label: string;
  types?: string;
  onChoose: (item: Item) => void;
}) {
  const { api } = useApp();
  const [query, setQuery] = useState('');
  const [search, setSearch] = useState('');
  const result = useLoad(
    () =>
      search
        ? api.items({ SearchTerm: search, Limit: 12, IncludeItemTypes: types })
        : Promise.resolve({ Items: [], TotalRecordCount: 0 }),
    [api, search, types],
  );
  return (
    <details className="media-picker">
      <summary>{label}</summary>
      <form
        className="filters"
        onSubmit={(e) => {
          e.preventDefault();
          setSearch(query);
        }}
      >
        <input
          aria-label={label + ' search'}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search your media"
        />
        <button>Search</button>
      </form>
      <Feedback busy={result.busy} error={result.error} />
      <div className="picker-results">
        {result.value?.Items.map((item) => (
          <button type="button" key={item.Id} onClick={() => onChoose(item)}>
            {api.image(item) && <img src={api.image(item)} alt="" />}
            <span>
              {item.Name}
              <small>
                {item.ProductionYear} · {item.Type}
              </small>
            </span>
          </button>
        ))}
      </div>
    </details>
  );
}
