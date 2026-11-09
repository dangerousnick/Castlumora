export interface User {
  Id: string;
  Name: string;
  Policy?: {
    IsAdministrator: boolean;
    EnableLiveTvAccess?: boolean;
    EnableLiveTvManagement?: boolean;
  };
  PrimaryImageTag?: string;
}
export interface Item {
  Id: string;
  Name: string;
  Type: string;
  Overview?: string;
  ProductionYear?: number;
  OfficialRating?: string;
  CommunityRating?: number;
  RunTimeTicks?: number;
  SeriesId?: string;
  SeriesName?: string;
  ParentIndexNumber?: number;
  IndexNumber?: number;
  ImageTags?: Record<string, string>;
  BackdropImageTags?: string[];
  UserData?: { PlaybackPositionTicks?: number; PlayedPercentage?: number; Played?: boolean };
  Genres?: string[];
  MediaSources?: MediaSource[];
  Chapters?: { Name?: string; StartPositionTicks: number; ImageTag?: string }[];
  MediaType?: string;
  Album?: string;
  AlbumId?: string;
  PlaylistItemId?: string;
  CanDelete?: boolean;
  Artists?: string[];
  CurrentProgram?: Item;
  StartDate?: string;
  EndDate?: string;
  ChannelId?: string;
}
export interface MediaStream {
  Index: number;
  Type: string;
  Codec?: string;
  Language?: string;
  DisplayTitle?: string;
  IsExternal?: boolean;
  DeliveryUrl?: string;
}
export interface MediaSource {
  Id: string;
  Name?: string;
  Container?: string;
  SupportsDirectPlay?: boolean;
  SupportsDirectStream?: boolean;
  TranscodingUrl?: string;
  LiveStreamId?: string;
  MediaStreams: MediaStream[];
  DefaultAudioStreamIndex?: number;
  DefaultSubtitleStreamIndex?: number;
}
export interface PlaybackInfo {
  MediaSources: MediaSource[];
  PlaySessionId: string;
  ErrorCode?: string;
}
export interface QueryResult {
  Items: Item[];
  TotalRecordCount: number;
}
export interface Session {
  server: string;
  token: string;
  user: User;
  deviceId: string;
}
export interface Preferences {
  watchlist: string[];
  hiddenResume: string[];
}
