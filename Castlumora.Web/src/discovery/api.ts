import { Api, sameId } from '../api/client';
import type { Item, QueryResult } from '../api/types';

export interface RecommendationGroup {
  CategoryId: string;
  BaselineItemName?: string;
  RecommendationType: string | number;
  Items: Item[];
}
export function viewer(api: Api) {
  const id = api.session?.user.Id;
  if (!id) throw new Error('Sign in to use personal discovery.');
  return id;
}
function validId(id: unknown): id is string {
  return (
    typeof id === 'string' &&
    /^[a-f0-9]{32}$/i.test(id.replaceAll('-', '')) &&
    !/^0{32}$/.test(id.replaceAll('-', ''))
  );
}
export async function genres(api: Api, startIndex = 0, favorites = false) {
  const limit = favorites ? 1000 : 100;
  const result = await api.request<QueryResult>(
    'Genres',
    {},
    {
      UserId: viewer(api),
      IncludeItemTypes: 'Movie',
      StartIndex: startIndex,
      Limit: limit,
      IsFavorite: favorites ? true : undefined,
      SortBy: 'SortName',
      SortOrder: 'Ascending',
      EnableImages: false,
      EnableTotalRecordCount: true,
    },
  );
  if (
    !Array.isArray(result.Items) ||
    result.Items.length > limit ||
    !Number.isSafeInteger(result.TotalRecordCount) ||
    result.TotalRecordCount < 0 ||
    result.Items.some(
      (item) =>
        !validId(item.Id) ||
        item.Type !== 'Genre' ||
        typeof item.Name !== 'string' ||
        !item.Name.trim(),
    )
  )
    throw new Error('Invalid genre response.');
  if (favorites && result.TotalRecordCount > limit)
    throw new Error(
      'More than 1000 favorite movie genres need review in the existing client; no interests were reset.',
    );
  return result;
}
export async function setGenreInterest(api: Api, id: string, enabled: boolean) {
  if (!validId(id)) throw new Error('Invalid genre.');
  const result = await api.request<{ IsFavorite: boolean }>(
    'UserFavoriteItems/' + encodeURIComponent(id),
    { method: enabled ? 'POST' : 'DELETE' },
    { UserId: viewer(api) },
  );
  if (result?.IsFavorite !== enabled)
    throw new Error('The server did not confirm the interest change.');
}
export async function recommendations(api: Api) {
  const result = await api.request<RecommendationGroup[]>(
    'Movies/Recommendations',
    {},
    { UserId: viewer(api), CategoryLimit: 4, ItemLimit: 8, Fields: 'Overview,Genres' },
  );
  if (
    !Array.isArray(result) ||
    result.length > 4 ||
    result.some(
      (group) =>
        !validId(group.CategoryId) || !Array.isArray(group.Items) || group.Items.length > 8,
    )
  )
    throw new Error('Invalid recommendation response.');
  return result;
}
const reasons = [
  'SimilarToRecentlyPlayed',
  'SimilarToLikedItem',
  'HasDirectorFromRecentlyPlayed',
  'HasActorFromRecentlyPlayed',
  'HasLikedDirector',
  'HasLikedActor',
];
export function reason(group: RecommendationGroup) {
  const type =
    typeof group.RecommendationType === 'number'
      ? reasons[group.RecommendationType]
      : group.RecommendationType;
  const labels: Record<string, string> = {
    SimilarToRecentlyPlayed: 'Because you watched',
    SimilarToLikedItem: 'Because you liked',
    HasDirectorFromRecentlyPlayed: 'Directors from your recent movies',
    HasActorFromRecentlyPlayed: 'Actors from your recent movies',
    HasLikedDirector: 'From a favorite director',
    HasLikedActor: 'From a favorite actor',
  };
  const label = labels[type] || 'Suggested by your server';
  return group.BaselineItemName ? label + ' — ' + group.BaselineItemName : label;
}
export function distinctMovies(items: Item[], seen: string[]) {
  return items.filter((item) => {
    if (
      !item ||
      typeof item.Name !== 'string' ||
      item.Type !== 'Movie' ||
      !validId(item.Id) ||
      seen.some((id) => sameId(id, item.Id))
    )
      return false;
    seen.push(item.Id);
    return true;
  });
}
