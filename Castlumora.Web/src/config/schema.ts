import { z } from 'zod';
const id = z.string().regex(/^[a-zA-Z0-9_-]{1,64}$/);
const mediaId = z
  .string()
  .regex(/^(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})$/);
const visibility = { userIds: z.array(mediaId).max(200).optional() };
const color = z.string().regex(/^#[0-9a-fA-F]{6}$/);
export const categorySchema = z.object({
  id,
  title: z.string().min(1).max(120),
  icon: z.string().max(120).optional(),
  color: color.optional(),
  artworkItemId: mediaId.optional(),
  ...visibility,
  rules: z.object({
    itemIds: z.array(mediaId).max(200),
    folderIds: z.array(mediaId).max(200),
    genres: z.array(z.string().min(1).max(128)).max(200),
    studios: z.array(z.string().min(1).max(128)).max(200),
    excludeIds: z.array(mediaId).max(200).optional(),
    contentTypes: z.array(z.enum(['Movie', 'Series', 'Episode'])).optional(),
  }),
});
export const rowSchema = z.object({
  id,
  title: z.string().min(1).max(120),
  kind: z.enum(['resume', 'next', 'recent', 'movies', 'series', 'category']),
  categoryId: id.optional(),
  ...visibility,
});
export const layoutSchema = z
  .object({
    schemaVersion: z.literal(1),
    revision: z.number().int().nonnegative(),
    theme: z.object({ accent: color, background: color }),
    navigation: z
      .array(
        z.object({
          id: z.enum(['home', 'browse', 'list', 'search']),
          label: z.string().min(1).max(120),
          ...visibility,
        }),
      )
      .max(4),
    featured: z.array(z.object({ itemId: mediaId, ...visibility })).max(20),
    rows: z.array(rowSchema).max(40),
    categories: z.array(categorySchema).max(100),
    devices: z.record(
      z.enum(['desktop', 'tablet', 'mobile', 'tv']),
      z.object({
        posterWidth: z.number().int().min(100).max(320).optional(),
        rowIds: z.array(id).max(40).optional(),
        safeZone: z.number().min(0).max(10).optional(),
      }),
    ),
  })
  .superRefine((layout, ctx) => {
    for (const key of ['navigation', 'rows', 'categories'] as const) {
      const ids = layout[key].map((v) => v.id);
      if (new Set(ids).size !== ids.length)
        ctx.addIssue({ code: 'custom', message: `Duplicate ${key} IDs` });
    }
    for (const row of layout.rows)
      if (row.kind === 'category' && !layout.categories.some((c) => c.id === row.categoryId))
        ctx.addIssue({ code: 'custom', message: 'Unknown category reference' });
    for (const device of Object.values(layout.devices))
      if (device?.rowIds?.some((id) => !layout.rows.some((r) => r.id === id)))
        ctx.addIssue({ code: 'custom', message: 'Unknown device row' });
  });
export type Layout = z.infer<typeof layoutSchema>;
export type Category = z.infer<typeof categorySchema>;
export type RowDefinition = z.infer<typeof rowSchema>;
export const defaultLayout: Layout = {
  schemaVersion: 1,
  revision: 0,
  theme: { accent: '#ad70ff', background: '#0d0b14' },
  navigation: [
    { id: 'home', label: 'Home' },
    { id: 'browse', label: 'Browse' },
    { id: 'list', label: 'My List' },
    { id: 'search', label: 'Search' },
  ],
  featured: [],
  rows: [
    { id: 'resume', title: 'Continue Watching', kind: 'resume' },
    { id: 'next', title: 'Up Next', kind: 'next' },
    { id: 'recent', title: 'Fresh arrivals', kind: 'recent' },
    { id: 'movies', title: 'Movie night starts here', kind: 'movies' },
    { id: 'series', title: 'Your next obsession', kind: 'series' },
  ],
  categories: [],
  devices: {},
};
export function parsePublished(value: unknown): Layout {
  if (!value || typeof value !== 'object') return structuredClone(defaultLayout);
  const raw = structuredClone(value) as Record<string, unknown>;
  if (Array.isArray(raw.rows)) raw.rows = raw.rows.filter((r) => rowSchema.safeParse(r).success);
  const parsed = layoutSchema.safeParse(raw);
  return parsed.success ? parsed.data : structuredClone(defaultLayout);
}
export function deviceClass(width = window.innerWidth): 'mobile' | 'tablet' | 'desktop' {
  return width < 640 ? 'mobile' : width < 1024 ? 'tablet' : 'desktop';
}
export function orderedRows(layout: Layout, device: string) {
  const ids = layout.devices[device as keyof Layout['devices']]?.rowIds;
  return ids ? ids.flatMap((id) => layout.rows.filter((r) => r.id === id)) : layout.rows;
}
