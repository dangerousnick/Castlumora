using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Server.Implementations.EntryPoints;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.EntryPoints;

public class UserDataChangeNotifierTests
{
    // How long a test waits for the notifier's timer callback to run. Generous: the assertions are
    // about a batch being sent at all, not about how promptly.
    private static readonly TimeSpan _flushTimeout = TimeSpan.FromSeconds(15);

    private readonly Mock<IUserDataManager> _userDataManager = new();
    private readonly Mock<ISessionManager> _sessionManager = new();
    private readonly Mock<IUserManager> _userManager = new();

    private readonly ConcurrentQueue<UserDataChangeInfo> _messages = new();
    private int _flushCount;

    public UserDataChangeNotifierTests()
    {
        _userManager.Setup(manager => manager.GetUserById(It.IsAny<Guid>()))
            .Returns((Guid id) => new User("Notifier fixture", "auth", "reset") { Id = id });
        _userDataManager.Setup(manager => manager.GetUserDataDto(It.IsAny<BaseItem>(), It.IsAny<User>()))
            .Returns(() => new UserItemDataDto { Key = "owned-user-data", PlaybackPositionTicks = 123, IsFavorite = true });
        _sessionManager
            .Setup(e => e.SendMessageToUserSessions(
                It.IsAny<System.Collections.Generic.List<Guid>>(),
                SessionMessageType.UserDataChanged,
                It.IsAny<Func<UserDataChangeInfo>>(),
                It.IsAny<CancellationToken>()))
            .Callback((List<Guid> _, SessionMessageType _, Func<UserDataChangeInfo> payload, CancellationToken _) =>
            {
                _messages.Enqueue(payload());
                Interlocked.Increment(ref _flushCount);
            })
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task OnUserDataSaved_ChangesNeverPause_StillSendsOnTheWindow()
    {
        // A scan changes user data continuously. The window must run from the first change of a batch,
        // or the batch never closes and holds every item it named alive for the length of the scan.
        var notifier = CreateNotifier();
        await notifier.StartAsync(TestContext.Current.CancellationToken);

        var userId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < _flushTimeout && Volatile.Read(ref _flushCount) == 0)
        {
            // Well below the window, and well below the size cap over the whole loop.
            RaiseUserDataSaved(userId);
            await Task.Delay(25, TestContext.Current.CancellationToken);
        }

        Assert.True(Volatile.Read(ref _flushCount) > 0, "The batch was never sent while changes kept arriving.");

        await notifier.StopAsync(TestContext.Current.CancellationToken);
        notifier.Dispose();
    }

    [Fact]
    public async Task Timer_DeletedRecipientDoesNotCrashOrBlockOtherUsers()
    {
        var deleted = Guid.NewGuid();
        var retained = Guid.NewGuid();
        _userManager.Setup(manager => manager.GetUserById(deleted)).Returns((User?)null);
        var delivered = new TaskCompletionSource<UserDataChangeInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var attemptedRecipients = new ConcurrentQueue<Guid[]>();
        _sessionManager.Setup(manager => manager.SendMessageToUserSessions(
            It.IsAny<List<Guid>>(), SessionMessageType.UserDataChanged, It.IsAny<Func<UserDataChangeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns((List<Guid> users, SessionMessageType _, Func<UserDataChangeInfo> payload, CancellationToken _) =>
            {
                attemptedRecipients.Enqueue(users.ToArray());
                delivered.TrySetResult(payload());
                return Task.CompletedTask;
            });
        using var notifier = CreateNotifier();
        await notifier.StartAsync(TestContext.Current.CancellationToken);
        RaiseUserDataSaved(deleted);
        RaiseUserDataSaved(retained);
        var message = await delivered.Task.WaitAsync(_flushTimeout, TestContext.Current.CancellationToken);
        Assert.Equal(retained, message.UserId);
        Assert.Equal(retained, Assert.Single(Assert.Single(attemptedRecipients)));
        var item = Assert.Single(message.UserDataList);
        Assert.NotEqual(Guid.Empty, item.ItemId);
        Assert.Equal(123, item.PlaybackPositionTicks);
        Assert.True(item.IsFavorite);
        await notifier.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeferredPayload_UsesCapturedIdentityWhenUserDisappearsDuringDelivery()
    {
        var userId = Guid.NewGuid();
        UserDataChangeInfo? delivered = null;
        _sessionManager.Setup(manager => manager.SendMessageToUserSessions(
            It.IsAny<List<Guid>>(), SessionMessageType.UserDataChanged, It.IsAny<Func<UserDataChangeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns((List<Guid> _, SessionMessageType _, Func<UserDataChangeInfo> payload, CancellationToken _) =>
            {
                _userManager.Setup(manager => manager.GetUserById(userId)).Returns((User?)null);
                delivered = payload();
                return Task.CompletedTask;
            });
        using var notifier = CreateNotifier();
        await notifier.StartAsync(TestContext.Current.CancellationToken);
        RaiseUserDataSaved(userId);
        await notifier.FlushAsync();
        Assert.NotNull(delivered);
        Assert.Equal(userId, delivered.UserId);
        Assert.Single(delivered.UserDataList);
        await notifier.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DeliveryFailure_IsLoggedAndDoesNotLoseOtherOrFutureBatches()
    {
        var failed = Guid.NewGuid();
        var retained = Guid.NewGuid();
        var logger = new Mock<ILogger<UserDataChangeNotifier>>();
        var delivered = new List<UserDataChangeInfo>();
        _sessionManager.Setup(manager => manager.SendMessageToUserSessions(
            It.IsAny<List<Guid>>(), SessionMessageType.UserDataChanged, It.IsAny<Func<UserDataChangeInfo>>(), It.IsAny<CancellationToken>()))
            .Returns((List<Guid> users, SessionMessageType _, Func<UserDataChangeInfo> payload, CancellationToken _) =>
            {
                if (users.Contains(failed))
                {
                    return Task.FromException(new IOException("Owned notification failure"));
                }

                delivered.Add(payload());
                return Task.CompletedTask;
            });
        using var notifier = new UserDataChangeNotifier(_userDataManager.Object, _sessionManager.Object, _userManager.Object, logger.Object);
        await notifier.StartAsync(TestContext.Current.CancellationToken);
        RaiseUserDataSaved(failed);
        RaiseUserDataSaved(retained);
        await notifier.FlushAsync();
        Assert.Equal(retained, Assert.Single(delivered).UserId);
        RaiseUserDataSaved(retained);
        await notifier.FlushAsync();
        Assert.Equal(2, delivered.Count);
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        await notifier.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PayloadFailure_IsLoggedAndLaterRecipientsStillReceiveChanges()
    {
        var failed = Guid.NewGuid();
        var retained = Guid.NewGuid();
        var logger = new Mock<ILogger<UserDataChangeNotifier>>();
        _userDataManager.Setup(manager => manager.GetUserDataDto(It.IsAny<BaseItem>(), It.Is<User>(user => user.Id.Equals(failed))))
            .Throws(new IOException("Owned DTO read failure"));
        using var notifier = new UserDataChangeNotifier(_userDataManager.Object, _sessionManager.Object, _userManager.Object, logger.Object);
        await notifier.StartAsync(TestContext.Current.CancellationToken);
        RaiseUserDataSaved(failed);
        RaiseUserDataSaved(retained);
        await notifier.FlushAsync();
        Assert.Equal(retained, Assert.Single(_messages).UserId);
        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        await notifier.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Stop_CancelsPendingBatchAndIgnoresLaterChanges()
    {
        using var notifier = CreateNotifier();
        await notifier.StartAsync(TestContext.Current.CancellationToken);
        RaiseUserDataSaved(Guid.NewGuid());
        await notifier.StopAsync(TestContext.Current.CancellationToken);
        await notifier.FlushAsync();
        RaiseUserDataSaved(Guid.NewGuid());
        await notifier.FlushAsync();
        Assert.Empty(_messages);
        Assert.Equal(0, _flushCount);
    }

    private UserDataChangeNotifier CreateNotifier()
        => new(_userDataManager.Object, _sessionManager.Object, _userManager.Object);

    // A folder needs none of BaseItem's static services, and PlaybackProgress is the one reason the
    // notifier ignores outright.
    private void RaiseUserDataSaved(Guid userId)
        => _userDataManager.Raise(
            e => e.UserDataSaved += null,
            _userDataManager.Object,
            new UserDataSaveEventArgs
            {
                UserId = userId,
                SaveReason = UserDataSaveReason.UpdateUserRating,
                Item = new Folder { Id = Guid.NewGuid() }
            });
}
