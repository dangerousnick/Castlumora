using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Emby.Server.Implementations.EntryPoints
{
    /// <summary>
    /// <see cref="IHostedService"/> responsible for notifying users when associated item data is updated.
    /// </summary>
    public sealed class UserDataChangeNotifier : IHostedService, IDisposable
    {
        private const int UpdateDuration = 500;
        internal const int MaxBatchSize = 2000;

        private readonly ISessionManager _sessionManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IUserManager _userManager;
        private readonly ILogger<UserDataChangeNotifier> _logger;

        private readonly Dictionary<Guid, Dictionary<Guid, BaseItem>> _changedItems = [];
        private readonly Lock _syncLock = new();

        private Timer? _updateTimer;
        private int _changedItemCount;
        private bool _stopped;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDataChangeNotifier"/> class.
        /// </summary>
        /// <param name="userDataManager">The <see cref="IUserDataManager"/>.</param>
        /// <param name="sessionManager">The <see cref="ISessionManager"/>.</param>
        /// <param name="userManager">The <see cref="IUserManager"/>.</param>
        public UserDataChangeNotifier(
            IUserDataManager userDataManager,
            ISessionManager sessionManager,
            IUserManager userManager)
            : this(userDataManager, sessionManager, userManager, NullLogger<UserDataChangeNotifier>.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDataChangeNotifier"/> class with background-delivery logging.
        /// </summary>
        /// <param name="userDataManager">User data changes.</param>
        /// <param name="sessionManager">Existing user-scoped delivery.</param>
        /// <param name="userManager">Current user identities.</param>
        /// <param name="logger">Background-delivery diagnostics.</param>
        public UserDataChangeNotifier(
            IUserDataManager userDataManager,
            ISessionManager sessionManager,
            IUserManager userManager,
            ILogger<UserDataChangeNotifier> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _userDataManager = userDataManager;
            _sessionManager = sessionManager;
            _userManager = userManager;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            lock (_syncLock)
            {
                _stopped = false;
            }

            _userDataManager.UserDataSaved += OnUserDataManagerUserDataSaved;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _userDataManager.UserDataSaved -= OnUserDataManagerUserDataSaved;
            ClearPending();
            return Task.CompletedTask;
        }

        private void OnUserDataManagerUserDataSaved(object? sender, UserDataSaveEventArgs e)
        {
            if (e.SaveReason == UserDataSaveReason.PlaybackProgress)
            {
                return;
            }

            lock (_syncLock)
            {
                if (_stopped)
                {
                    return;
                }

                // The window runs from the first change of a batch and is never extended, so a stream
                // of changes that never pauses - a library scan - still closes its batches instead of
                // holding every item it touched alive until the stream stops.
                _updateTimer ??= new Timer(
                    UpdateTimerCallback,
                    null,
                    UpdateDuration,
                    Timeout.Infinite);

                if (!_changedItems.TryGetValue(e.UserId, out Dictionary<Guid, BaseItem>? keys))
                {
                    keys = [];
                    _changedItems[e.UserId] = keys;
                }

                var baseItem = e.Item;

                // Go up one level for indicators
                if (baseItem is not null)
                {
                    Track(keys, baseItem);

                    var parent = baseItem.GetOwner() ?? baseItem.GetParent();

                    if (parent is not null)
                    {
                        Track(keys, parent);
                    }
                }

                // A window long enough to cover a burst still has to give way once the batch is
                // large enough to be worth sending on its own.
                if (_changedItemCount >= MaxBatchSize)
                {
                    _updateTimer.Change(0, Timeout.Infinite);
                }
            }
        }

        private void Track(Dictionary<Guid, BaseItem> keys, BaseItem item)
        {
            var before = keys.Count;
            keys[item.Id] = item;

            if (keys.Count != before)
            {
                _changedItemCount++;
            }
        }

        private async void UpdateTimerCallback(object? state)
        {
            try
            {
                await FlushAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // A timer callback must never terminate the server on notification failure.
                _logger.LogError(exception, "Error processing queued user data notifications");
            }
        }

        private List<KeyValuePair<Guid, Dictionary<Guid, BaseItem>>> TakePendingBatch()
        {
            List<KeyValuePair<Guid, Dictionary<Guid, BaseItem>>> changes;
            lock (_syncLock)
            {
                if (_stopped)
                {
                    return [];
                }

                changes = _changedItems.ToList();
                _changedItems.Clear();
                _changedItemCount = 0;

                if (_updateTimer is not null)
                {
                    // Non-waiting cancellation: awaiting disposal from this timer's own
                    // callback would wait for the callback itself. Keep the batch drain atomic.
                    _updateTimer.Dispose();
                    _updateTimer = null;
                }
            }

            return changes;
        }

        internal async Task FlushAsync()
        {
            var changes = TakePendingBatch();

            if (changes.Count == 0)
            {
                return;
            }

            foreach (var (userId, changedItems) in changes)
            {
                if (Volatile.Read(ref _stopped))
                {
                    return;
                }

                try
                {
                    // Playback can queue an update just before this user is deleted.
                    var user = _userManager.GetUserById(userId);
                    if (user is null)
                    {
                        continue;
                    }

                    // Capture the identity before lazy session delivery; deletion must not cause
                    // a second lookup to throw from the timer's async-void entry point.
                    await _sessionManager.SendMessageToUserSessions(
                        [userId],
                        SessionMessageType.UserDataChanged,
                        () => GetUserDataChangeInfo(user, changedItems.Values),
                        default).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error sending user data notifications for user {UserId}", userId);
                }
            }
        }

        private UserDataChangeInfo GetUserDataChangeInfo(User user, IEnumerable<BaseItem> changedItems)
        {
            return new UserDataChangeInfo
            {
                UserId = user.Id,
                UserDataList = changedItems
                    .Select(i =>
                    {
                        var dto = _userDataManager.GetUserDataDto(i, user);
                        if (dto is null)
                        {
                            return null!;
                        }

                        dto.ItemId = i.Id;
                        return dto;
                    })
                    .Where(e => e is not null)
                    .ToArray()
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _userDataManager.UserDataSaved -= OnUserDataManagerUserDataSaved;
            ClearPending();
        }

        private void ClearPending()
        {
            lock (_syncLock)
            {
                _stopped = true;
                _updateTimer?.Dispose();
                _updateTimer = null;
                _changedItems.Clear();
                _changedItemCount = 0;
            }
        }
    }
}
