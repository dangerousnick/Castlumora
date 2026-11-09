import { describe, expect, it } from 'vitest';
import { defaultLayout, layoutSchema, parsePublished, orderedRows } from './schema';
describe('presentation contract', () => {
  it('accepts the shared default', () =>
    expect(layoutSchema.parse(defaultLayout)).toEqual(defaultLayout));
  it('rejects executable theme values and duplicate rows', () => {
    expect(
      layoutSchema.safeParse({
        ...defaultLayout,
        theme: { accent: 'url(javascript:x)', background: '#000000' },
      }).success,
    ).toBe(false);
    expect(
      layoutSchema.safeParse({
        ...defaultLayout,
        rows: [defaultLayout.rows[0], defaultLayout.rows[0]],
      }).success,
    ).toBe(false);
  });
  it('rejects missing categories and invalid device row references', () => {
    expect(
      layoutSchema.safeParse({
        ...defaultLayout,
        rows: [{ id: 'x', title: 'X', kind: 'category', categoryId: 'absent' }],
      }).success,
    ).toBe(false);
    expect(
      layoutSchema.safeParse({ ...defaultLayout, devices: { mobile: { rowIds: ['absent'] } } })
        .success,
    ).toBe(false);
  });
  it('skips unsupported component kinds and falls back for unknown major versions', () => {
    expect(
      parsePublished({
        ...defaultLayout,
        rows: [...defaultLayout.rows, { id: 'plugin', title: 'Unknown', kind: 'plugin:future' }],
      }).rows,
    ).toHaveLength(defaultLayout.rows.length);
    expect(parsePublished({ ...defaultLayout, schemaVersion: 100 })).toEqual(defaultLayout);
  });
  it('uses published device order', () => {
    const config = { ...defaultLayout, devices: { mobile: { rowIds: ['movies', 'resume'] } } };
    expect(orderedRows(config, 'mobile').map((r) => r.id)).toEqual(['movies', 'resume']);
  });
});
