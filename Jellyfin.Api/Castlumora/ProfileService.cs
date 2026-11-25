using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Users;

namespace Jellyfin.Api.Castlumora;

/// <summary>Managed viewing identities with private ownership and full inherited authentication.</summary>
public sealed class ProfileService : IDisposable
{
    private readonly string _root;
    private readonly IUserManager _users;
    private readonly ILibraryManager _libraries;
    private readonly ISessionManager _sessions;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private State? _lastGood;
    private DateTime _lastWrite;
    private long _lastLength;

    /// <summary>Initializes a new instance of the <see cref="ProfileService"/> class.</summary>
    /// <param name="paths">Persistent private configuration.</param>
    /// <param name="users">Existing password, policy and user-state manager.</param>
    /// <param name="libraries">Real library registry.</param>
    /// <param name="sessions">Existing device/session token lifecycle.</param>
    public ProfileService(IApplicationPaths paths, IUserManager users, ILibraryManager libraries, ISessionManager sessions)
    {
        _root = Path.Combine(paths.ConfigurationDirectoryPath, "castlumora", "profiles");
        _users = users;
        _libraries = libraries;
        _sessions = sessions;
    }

    /// <summary>Checks managed identities without changing access for ordinary inherited accounts.</summary>
    /// <param name="caller">Authenticated identity.</param>
    /// <param name="token">Cancellation.</param>
    /// <returns>Whether a linked profile still has an enabled owner and valid configuration.</returns>
    public async Task<bool> SessionAllowedAsync(Guid caller, CancellationToken token)
    {
        var user = _users.GetUserById(caller);
        if (user is null)
        {
            return false;
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var profile = Read().Profiles.SingleOrDefault(profile => profile.User.Equals(caller));
            if (profile is null)
            {
                return !ManagedName(user.Username);
            }

            var owner = _users.GetUserById(profile.Owner);
            var key = Path.Combine(_root, "credentials.key");
            YtDlpPlatform.RejectLink(key);
            if (!profile.Ready || owner is null || owner.HasPermission(PermissionKind.IsDisabled)
                || user.HasPermission(PermissionKind.IsDisabled) || user.HasPermission(PermissionKind.IsAdministrator)
                || !File.Exists(key) || new FileInfo(key).Length != 32)
            {
                return false;
            }

            var parent = _users.GetUserDto(owner).Policy;
            var child = _users.GetUserDto(user).Policy;
            return WithinPolicy(parent, child);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            // Preserve inherited accounts during profile-only damage; known managed users fail closed.
            return !ManagedName(user.Username) && !(_lastGood?.Profiles.Any(profile => profile.User.Equals(caller)) ?? false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lists only the authenticated account's viewing identities.</summary>
    /// <param name="caller">Authenticated user ID.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Safe profile names and IDs, never managed credentials.</returns>
    public async Task<JsonObject> ListAsync(Guid caller, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            var owner = Owner(state, caller);
            var account = _users.GetUserById(owner) ?? throw new UnauthorizedAccessException();
            if (account.HasPermission(PermissionKind.IsDisabled))
            {
                throw new UnauthorizedAccessException();
            }

            var profiles = new JsonArray(new JsonObject { ["id"] = owner.ToString("N"), ["name"] = account.Username, ["preset"] = "account", ["ready"] = true });
            foreach (var profile in state.Profiles.Where(profile => profile.Owner.Equals(owner)))
            {
                var user = _users.GetUserById(profile.User);
                if (user is not null)
                {
                    profiles.Add(new JsonObject { ["id"] = profile.User.ToString("N"), ["name"] = profile.Name, ["preset"] = profile.Preset, ["ready"] = profile.Ready && !user.HasPermission(PermissionKind.IsDisabled) && !user.HasPermission(PermissionKind.IsAdministrator) });
                }
            }

            return new JsonObject { ["accountId"] = owner.ToString("N"), ["accountName"] = account.Username, ["profiles"] = profiles };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs a sign-in operation only for a profile owned by the authenticated account.</summary>
    /// <typeparam name="T">Safe operation result.</typeparam>
    /// <param name="caller">Authenticated owning identity, never a requested account override.</param>
    /// <param name="identity">Registered child profile.</param>
    /// <param name="operation">Existing scoped session operation, serialized with registry changes.</param>
    /// <param name="token">Cancellation before the operation begins.</param>
    /// <returns>The operation result without changing profile lifecycle or credentials.</returns>
    public async Task<T> UseOwnedProfileSessionsAsync<T>(Guid caller, Guid identity, Func<string, Task<T>> operation, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            RequireOwningAccount(state, caller);

            var profile = state.Profiles.SingleOrDefault(profile => profile.User.Equals(identity)
                && profile.Owner.Equals(caller) && !profile.User.Equals(caller));
            if (profile is null)
            {
                throw new KeyNotFoundException();
            }

            token.ThrowIfCancellationRequested();
            // Unavailable/pending profiles can still have old tokens; never reactivate them here.
            return await operation(profile.Name).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Runs account-wide session containment against server-derived child identities.</summary>
    /// <typeparam name="T">Safe operation result.</typeparam>
    /// <param name="caller">Authenticated owning account.</param>
    /// <param name="operation">Operation serialized with profile lifecycle changes.</param>
    /// <param name="token">Cancellation before revocation begins.</param>
    /// <returns>The result without changing credentials or registry state.</returns>
    public async Task<T> UseOwnedAccountSessionsAsync<T>(Guid caller, Func<Guid[], Task<T>> operation, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            RequireOwningAccount(state, caller);
            var children = state.Profiles.Where(profile => profile.Owner.Equals(caller) && !profile.User.Equals(caller))
                .Select(profile => profile.User).ToArray();
            token.ThrowIfCancellationRequested();
            return await operation(children).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RequireOwningAccount(State state, Guid caller)
    {
        var account = _users.GetUserById(caller);
        if (account is null || account.HasPermission(PermissionKind.IsDisabled) || ManagedName(account.Username)
            || state.Profiles.Any(profile => profile.User.Equals(caller)))
        {
            throw new UnauthorizedAccessException();
        }
    }

    /// <summary>Checks whether a ready managed profile's owner permits media preference editing.</summary>
    /// <param name="identity">Identity supplied by the authenticated caller, not a requested target.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Whether the enabled owner permits preference editing for this managed identity.</returns>
    public async Task<bool> CanEditManagedPreferencesAsync(Guid identity, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var profile = Read().Profiles.SingleOrDefault(profile => profile.User.Equals(identity) && profile.Ready);
            var owner = profile is null ? null : _users.GetUserById(profile.Owner);
            return owner is not null && !owner.HasPermission(PermissionKind.IsDisabled)
                && (owner.HasPermission(PermissionKind.IsAdministrator) || owner.EnableUserPreferenceAccess);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Lists managed identities for administrator review, including pending/orphaned records.</summary>
    /// <param name="actor">Authenticated administrator.</param>
    /// <param name="token">Request cancellation.</param>
    /// <returns>Safe identity and ownership information, never credentials.</returns>
    public async Task<JsonObject> ListManagedAsync(Guid actor, CancellationToken token)
    {
        RequireAdministrator(actor);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var entries = new JsonArray();
            foreach (var profile in Read().Profiles)
            {
                var owner = _users.GetUserById(profile.Owner);
                var user = _users.GetUserById(profile.User);
                var ownerPolicy = owner is null ? null : _users.GetUserDto(owner).Policy;
                var childPolicy = user is null ? null : _users.GetUserDto(user).Policy;
                entries.Add(new JsonObject
                {
                    ["id"] = profile.User.ToString("N"),
                    ["accountId"] = profile.Owner.ToString("N"),
                    ["accountName"] = owner?.Username ?? "Deleted account",
                    ["name"] = profile.Name,
                    ["preset"] = profile.Preset,
                    ["libraries"] = JsonSerializer.SerializeToNode(childPolicy?.EnabledFolders ?? Array.Empty<Guid>(), JsonDefaults.Options),
                    ["accessRevision"] = ownerPolicy is not null && childPolicy is not null ? AccessRevision(profile, childPolicy, ownerPolicy) : null,
                    ["exists"] = user is not null,
                    ["disabled"] = user is null || user.HasPermission(PermissionKind.IsDisabled),
                    ["lifecycle"] = profile.Lifecycle,
                    ["ready"] = profile.Ready && owner is not null && user is not null
                        && !owner.HasPermission(PermissionKind.IsDisabled)
                        && !user.HasPermission(PermissionKind.IsDisabled)
                        && !user.HasPermission(PermissionKind.IsAdministrator)
                        && WithinPolicy(ownerPolicy!, childPolicy!)
                });
            }

            return new JsonObject { ["profiles"] = entries, ["recoveryAvailable"] = true, ["accessEditingAvailable"] = true };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Changes only a managed profile's display name, preserving its identity and history.</summary>
    /// <param name="actor">Authenticated administrator.</param>
    /// <param name="identity">Registered managed identity.</param>
    /// <param name="name">Unique display name within its owning account.</param>
    /// <param name="token">Cancellation before the atomic registry update.</param>
    /// <returns>False for an account or unknown profile.</returns>
    public async Task<bool> RenameAsync(Guid actor, Guid identity, string name, CancellationToken token)
    {
        RequireAdministrator(actor);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl))
        {
            throw new InvalidDataException("Choose a profile display name of up to forty characters.");
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            var index = state.Profiles.FindIndex(profile => profile.User.Equals(identity));
            if (index < 0)
            {
                return false;
            }

            var current = state.Profiles[index];
            name = name.Trim();
            if (state.Profiles.Any(profile => profile.Owner.Equals(current.Owner) && !profile.User.Equals(identity)
                && profile.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Choose a unique profile display name within this account.");
            }

            token.ThrowIfCancellationRequested();
            // The core username is a security marker; never rename it or rotate credentials here.
            state.Profiles[index] = current with { Name = name };
            Write(state);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Disables/revokes/deletes only a registered managed identity, then durably removes its mapping.</summary>
    /// <param name="actor">Authenticated administrator.</param>
    /// <param name="identity">Managed identity, never an owning account.</param>
    /// <param name="token">Cancellation before destructive work begins.</param>
    /// <returns>False when no managed mapping exists.</returns>
    public async Task<bool> DeleteAsync(Guid actor, Guid identity, CancellationToken token)
    {
        RequireAdministrator(actor);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            var index = state.Profiles.FindIndex(profile => profile.User.Equals(identity));
            if (index < 0)
            {
                return false;
            }

            token.ThrowIfCancellationRequested();
            // Persist denial first. A failure/crash leaves a retryable, unavailable record.
            state.Profiles[index] = state.Profiles[index] with { Ready = false, Lifecycle = "deleting", PendingLibraries = null };
            Write(state);
            var user = _users.GetUserById(identity);
            if (user is not null)
            {
                var policy = _users.GetUserDto(user).Policy;
                policy.IsDisabled = true;
                await _users.UpdatePolicyAsync(identity, policy).ConfigureAwait(false);
                await _sessions.RevokeUserTokens(identity, null).ConfigureAwait(false);
                await _users.DeleteUserAsync(identity).ConfigureAwait(false);
            }

            // Also repairs a mapping left after an earlier successful database deletion.
            state.Profiles.RemoveAt(index);
            Write(state);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Resumes only explicit interrupted provisioning using the original managed identity.</summary>
    /// <param name="actor">Authenticated administrator.</param>
    /// <param name="identity">Existing provisioning identity.</param>
    /// <param name="token">Cancellation before recovery mutations begin.</param>
    /// <returns>False for an unknown identity; never recreates a user or missing key.</returns>
    public async Task<bool> RecoverAsync(Guid actor, Guid identity, CancellationToken token)
    {
        RequireAdministrator(actor);
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            var index = state.Profiles.FindIndex(profile => profile.User.Equals(identity));
            if (index < 0)
            {
                return false;
            }

            var profile = state.Profiles[index];
            if (profile.Ready || profile.Lifecycle != "provisioning" || profile.PendingLibraries is not { Length: > 0 } folders)
            {
                throw new InvalidOperationException("Only explicit interrupted provisioning can be recovered.");
            }

            var account = _users.GetUserById(profile.Owner);
            var user = _users.GetUserById(identity);
            if (account is null || account.HasPermission(PermissionKind.IsDisabled) || string.IsNullOrEmpty(account.Password)
                || state.Profiles.Any(candidate => candidate.User.Equals(profile.Owner))
                || user is null || user.HasPermission(PermissionKind.IsAdministrator) || !ManagedName(user.Username))
            {
                throw new InvalidOperationException("Recovery requires an enabled owner and the original restricted managed identity.");
            }

            var ownerPolicy = _users.GetUserDto(account).Policy;
            var known = _libraries.GetVirtualFolders().Select(folder => Guid.TryParse(folder.ItemId, out var id) ? id : Guid.Empty).ToHashSet();
            if (folders.Any(folder => folder.Equals(Guid.Empty) || !known.Contains(folder)
                || (!ownerPolicy.EnableAllFolders && !ownerPolicy.EnabledFolders.Contains(folder))))
            {
                throw new InvalidOperationException("The original libraries must still exist and be permitted by the owner.");
            }

            var key = Key(state);
            token.ThrowIfCancellationRequested();
            await CompleteProvisioning(state, index, profile, user, ownerPolicy, folders, key).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reapplies an administrator-confirmed bounded preset and libraries to an existing profile.</summary>
    /// <param name="actor">Authenticated administrator.</param>
    /// <param name="identity">Existing managed identity, never a replacement account.</param>
    /// <param name="preset">Kids or adult preset.</param>
    /// <param name="folders">Explicit currently permitted libraries.</param>
    /// <param name="expectedRevision">Reviewed profile and owner policy revision.</param>
    /// <param name="token">Cancellation before the durable update begins.</param>
    /// <returns>Whether the profile exists; interrupted updates retain recoverable provisioning intent.</returns>
    public async Task<bool> UpdateAccessAsync(Guid actor, Guid identity, string preset, Guid[] folders, string expectedRevision, CancellationToken token)
    {
        RequireAdministrator(actor);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(expectedRevision);
        folders = folders.ToArray();
        if (preset is not ("kids" or "adult") || folders.Length is < 1 or > 100 || folders.Distinct().Count() != folders.Length
            || folders.Contains(Guid.Empty) || expectedRevision.Length != 64 || expectedRevision.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("Choose a preset, explicit libraries and a reviewed access revision.");
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            RequireAdministrator(actor);
            var state = Read();
            var index = state.Profiles.FindIndex(profile => profile.User.Equals(identity));
            if (index < 0)
            {
                return false;
            }

            var profile = state.Profiles[index];
            var owner = _users.GetUserById(profile.Owner);
            var user = _users.GetUserById(identity);
            if (!profile.Ready || profile.Lifecycle != "active" || owner is null || owner.HasPermission(PermissionKind.IsDisabled)
                || string.IsNullOrEmpty(owner.Password) || state.Profiles.Any(candidate => candidate.User.Equals(profile.Owner))
                || user is null || user.HasPermission(PermissionKind.IsDisabled) || user.HasPermission(PermissionKind.IsAdministrator) || !ManagedName(user.Username))
            {
                throw new InvalidOperationException("Review the existing enabled profile and owner; interrupted setup requires recovery.");
            }

            var ownerPolicy = _users.GetUserDto(owner).Policy;
            if (!string.Equals(expectedRevision, AccessRevision(profile, _users.GetUserDto(user).Policy, ownerPolicy), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Profile or owner access changed. Cancel access edit and reopen it to review current settings.");
            }

            var known = _libraries.GetVirtualFolders().Select(folder => Guid.TryParse(folder.ItemId, out var id) ? id : Guid.Empty).ToHashSet();
            if (folders.Any(folder => !known.Contains(folder) || (!ownerPolicy.EnableAllFolders && !ownerPolicy.EnabledFolders.Contains(folder))))
            {
                throw new ArgumentException("Select existing libraries permitted by the owning account.");
            }

            var key = Key(state);
            token.ThrowIfCancellationRequested();
            var planned = profile with { Preset = preset, Ready = false, Lifecycle = "provisioning", PendingLibraries = folders };
            state.Profiles[index] = planned;
            // Persist denial and the exact repair plan before disabling/revoking or changing native policy.
            Write(state);
            await CompleteProvisioning(state, index, planned, user, ownerPolicy, folders, key).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string AccessRevision(ViewingProfile profile, UserPolicy child, UserPolicy owner)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { profile.Owner, profile.User, profile.Preset, ChildPolicy = child, OwnerPolicy = owner }, JsonDefaults.Options)));

    private async Task CompleteProvisioning(State state, int index, ViewingProfile profile, User user, UserPolicy ownerPolicy, Guid[] folders, byte[] key)
    {
        // Durable denial already exists. Never expose a partially repaired identity as ready.
        var disabled = _users.GetUserDto(user).Policy;
        disabled.IsDisabled = true;
        await _users.UpdatePolicyAsync(user.Id, disabled).ConfigureAwait(false);
        await _sessions.RevokeUserTokens(user.Id, null).ConfigureAwait(false);
        await _users.ChangePassword(user.Id, Secret(key, profile)).ConfigureAwait(false);
        await _users.UpdatePolicyAsync(user.Id, BoundedPolicy(profile.Preset, folders, ownerPolicy, user)).ConfigureAwait(false);
        state.Profiles[index] = profile with { Ready = true, Lifecycle = "active", PendingLibraries = null };
        Write(state);
    }

    /// <summary>Creates a disabled-first managed viewing identity with explicit library access.</summary>
    /// <param name="actor">Authenticated administrator.</param>
    /// <param name="owner">Existing account identity.</param>
    /// <param name="name">Profile display name.</param>
    /// <param name="preset">Kids or adult preset.</param>
    /// <param name="folders">Explicit real library IDs.</param>
    /// <param name="token">Request cancellation before provisioning.</param>
    /// <returns>Created profile identity.</returns>
    public async Task<JsonObject> CreateAsync(Guid actor, Guid owner, string name, string preset, Guid[] folders, CancellationToken token)
    {
        if (_users.GetUserById(actor)?.HasPermission(PermissionKind.IsAdministrator) != true)
        {
            throw new UnauthorizedAccessException();
        }

        ArgumentNullException.ThrowIfNull(folders);
        folders = folders.ToArray();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || name.Any(char.IsControl) || preset is not ("kids" or "adult")
            || folders.Length is < 1 or > 100 || folders.Distinct().Count() != folders.Length)
        {
            throw new InvalidDataException("Choose a profile name, kids/adult preset and explicit libraries.");
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            var account = _users.GetUserById(owner) ?? throw new InvalidDataException("Account not found.");
            if (account.HasPermission(PermissionKind.IsDisabled) || string.IsNullOrEmpty(account.Password)
                || state.Profiles.Any(profile => profile.User.Equals(owner)) || state.Profiles.Count >= 1000
                || state.Profiles.Count(profile => profile.Owner.Equals(owner)) >= 10
                || state.Profiles.Any(profile => profile.Owner.Equals(owner) && profile.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Use an enabled password-protected account and a unique profile name; maximum ten profiles per account.");
            }

            var known = _libraries.GetVirtualFolders().Select(folder => Guid.TryParse(folder.ItemId, out var id) ? id : Guid.Empty).ToHashSet();
            var ownerPolicy = _users.GetUserDto(account).Policy;
            if (folders.Any(folder => !known.Contains(folder) || (!ownerPolicy.EnableAllFolders && !ownerPolicy.EnabledFolders.Contains(folder))))
            {
                throw new InvalidDataException("Select existing libraries available to the account.");
            }

            var planned = new ViewingProfile(owner, Guid.NewGuid(), name.Trim(), preset, false, "provisioning", folders.ToArray());
            var projected = new List<ViewingProfile>(state.Profiles) { planned };
            // Reserve room for 1,000 names of 40 escaped characters and deletion transitions.
            _ = Serialize(new State(2, projected), 2097152 - 262144);
            var key = Key(state);
            token.ThrowIfCancellationRequested();
            // Once provisioning begins, finish its durable reservation even if the HTTP client leaves.
            var user = await _users.CreateUserAsync("Castlumora profile " + Guid.NewGuid().ToString("N"), initiallyDisabled: true).ConfigureAwait(false);
            var profile = planned with { User = user.Id };
            state.Profiles.Add(profile);
            Write(state);
            await _users.ChangePassword(user.Id, Secret(key, profile)).ConfigureAwait(false);
            var policy = BoundedPolicy(preset, folders, ownerPolicy, user);
            await _users.UpdatePolicyAsync(user.Id, policy).ConfigureAwait(false);
            state.Profiles[state.Profiles.Count - 1] = profile with { Ready = true, Lifecycle = "active", PendingLibraries = null };
            Write(state);
            return new JsonObject { ["id"] = user.Id.ToString("N"), ["name"] = profile.Name, ["preset"] = profile.Preset, ["ready"] = true };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reauthenticates the account and replaces this device's selected viewing identity.</summary>
    /// <param name="caller">Current authenticated identity.</param>
    /// <param name="target">Selected identity in the same account.</param>
    /// <param name="password">Account password, never persisted.</param>
    /// <param name="auth">Current authenticated device information.</param>
    /// <param name="remote">Normalized remote address.</param>
    /// <param name="token">Request cancellation before token issuance.</param>
    /// <returns>A normal inherited authentication result scoped to the selected user.</returns>
    public async Task<AuthenticationResult> SwitchAsync(Guid caller, Guid target, string password, AuthorizationInfo auth, string remote, CancellationToken token)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024 || auth.IsApiKey || !auth.HasToken
            || string.IsNullOrWhiteSpace(auth.DeviceId) || string.IsNullOrWhiteSpace(auth.Client)
            || string.IsNullOrWhiteSpace(auth.Device) || string.IsNullOrWhiteSpace(auth.Version))
        {
            throw new UnauthorizedAccessException();
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var state = Read();
            var owner = Owner(state, caller);
            var account = _users.GetUserById(owner) ?? throw new UnauthorizedAccessException();
            var profile = state.Profiles.SingleOrDefault(profile => profile.Owner.Equals(owner) && profile.User.Equals(target));
            if (!target.Equals(owner) && (profile is null || !profile.Ready))
            {
                throw new UnauthorizedAccessException();
            }

            if (await _users.AuthenticateUser(account.Username, password, remote, true).ConfigureAwait(false) is not { } verified || !verified.Id.Equals(owner))
            {
                throw new AuthenticationException("Account verification failed.");
            }

            var selected = _users.GetUserById(target) ?? throw new UnauthorizedAccessException();
            if (selected.HasPermission(PermissionKind.IsDisabled) || (!target.Equals(owner) && selected.HasPermission(PermissionKind.IsAdministrator)))
            {
                throw new UnauthorizedAccessException();
            }

            if (profile is not null && !WithinPolicy(_users.GetUserDto(account).Policy, _users.GetUserDto(selected).Policy))
            {
                throw new UnauthorizedAccessException();
            }

            token.ThrowIfCancellationRequested();
            var result = await _sessions.AuthenticateNewSession(new AuthenticationRequest
            {
                Username = selected.Username,
                Password = profile is null ? password : Secret(Key(state), profile),
                App = auth.Client,
                AppVersion = auth.Version,
                DeviceId = auth.DeviceId,
                DeviceName = auth.Device,
                RemoteEndPoint = remote
            }).ConfigureAwait(false);
            // Never leave the old parent token cached and reusable after entering a kids profile.
            if (!target.Equals(caller))
            {
                try
                {
                    await _sessions.Logout(auth.Token).ConfigureAwait(false);
                }
                catch
                {
                    await _sessions.Logout(result.AccessToken).ConfigureAwait(false);
                    throw;
                }
            }

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    internal static UserPolicy Preset(string preset, Guid[] folders) => new()
    {
        IsAdministrator = false,
        IsHidden = true,
        IsDisabled = false,
        EnableUserPreferenceAccess = false,
        EnableAllFolders = false,
        EnabledFolders = folders,
        EnableAllChannels = false,
        EnabledChannels = [],
        EnableLiveTvAccess = false,
        EnableLiveTvManagement = false,
        EnableContentDeletion = false,
        EnableContentDownloading = preset == "adult",
        EnablePublicSharing = false,
        EnableSharedDeviceControl = false,
        EnableCollectionManagement = false,
        EnableMediaConversion = false,
        MaxParentalRating = preset == "kids" ? 6 : null,
        BlockUnratedItems = preset == "kids" ? Enum.GetValues<UnratedItem>() : []
    };

    private static UserPolicy BoundedPolicy(string preset, Guid[] folders, UserPolicy ownerPolicy, User user)
    {
        var policy = Preset(preset, folders);
        // Keep the identity's inherited providers; a new UserPolicy has null provider IDs.
        policy.AuthenticationProviderId = user.AuthenticationProviderId;
        policy.PasswordResetProviderId = user.PasswordResetProviderId;
        policy.EnableRemoteAccess = ownerPolicy.EnableRemoteAccess;
        policy.EnableMediaPlayback = ownerPolicy.EnableMediaPlayback;
        policy.EnableAudioPlaybackTranscoding = ownerPolicy.EnableAudioPlaybackTranscoding;
        policy.EnableVideoPlaybackTranscoding = ownerPolicy.EnableVideoPlaybackTranscoding;
        policy.EnablePlaybackRemuxing = ownerPolicy.EnablePlaybackRemuxing;
        policy.ForceRemoteSourceTranscoding = ownerPolicy.ForceRemoteSourceTranscoding;
        policy.EnableContentDownloading &= ownerPolicy.EnableContentDownloading;
        policy.EnableSyncTranscoding = false;
        policy.MaxParentalRating = ownerPolicy.MaxParentalRating is { } maximum ? Math.Min(policy.MaxParentalRating ?? maximum, maximum) : policy.MaxParentalRating;
        policy.MaxParentalSubRating = ownerPolicy.MaxParentalSubRating;
        policy.BlockUnratedItems = policy.BlockUnratedItems.Union(ownerPolicy.BlockUnratedItems).ToArray();
        policy.BlockedTags = ownerPolicy.BlockedTags.ToArray();
        policy.AllowedTags = ownerPolicy.AllowedTags.ToArray();
        policy.BlockedMediaFolders = ownerPolicy.BlockedMediaFolders?.ToArray() ?? [];
        policy.EnableAllDevices = ownerPolicy.EnableAllDevices;
        policy.EnabledDevices = ownerPolicy.EnabledDevices.ToArray();
        policy.RemoteClientBitrateLimit = ownerPolicy.RemoteClientBitrateLimit;
        policy.MaxActiveSessions = ownerPolicy.MaxActiveSessions;
        policy.AccessSchedules = ownerPolicy.AccessSchedules.Select(schedule => new AccessSchedule(schedule.DayOfWeek, schedule.StartHour, schedule.EndHour, user.Id)).ToArray();
        return policy;
    }

    private static bool ManagedName(string name) => name.StartsWith("Castlumora profile ", StringComparison.Ordinal)
        && Guid.TryParseExact(name["Castlumora profile ".Length..], "N", out _);

    private void RequireAdministrator(Guid actor)
    {
        if (_users.GetUserById(actor)?.HasPermission(PermissionKind.IsAdministrator) != true)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private static bool WithinPolicy(UserPolicy parent, UserPolicy child)
    {
        var permissions = new[]
        {
            (child.EnableRemoteAccess, parent.EnableRemoteAccess),
            (child.EnableMediaPlayback, parent.EnableMediaPlayback),
            (child.EnableAudioPlaybackTranscoding, parent.EnableAudioPlaybackTranscoding),
            (child.EnableVideoPlaybackTranscoding, parent.EnableVideoPlaybackTranscoding),
            (child.EnablePlaybackRemuxing, parent.EnablePlaybackRemuxing),
            (child.EnableContentDownloading, parent.EnableContentDownloading)
        };
        return permissions.All(permission => !permission.Item1 || permission.Item2)
            && !child.EnableLiveTvAccess && !child.EnableLiveTvManagement && !child.EnableContentDeletion
            && !child.EnablePublicSharing && !child.EnableSharedDeviceControl && !child.EnableCollectionManagement && !child.EnableUserPreferenceAccess
            && !child.EnableRemoteControlOfOtherUsers && !child.EnableSubtitleManagement && !child.EnableLyricManagement
            && !child.EnableMediaConversion && !child.EnableSyncTranscoding && !child.EnableAllFolders
            && (!parent.ForceRemoteSourceTranscoding || child.ForceRemoteSourceTranscoding)
            && (parent.EnableAllFolders || (!child.EnableAllFolders && child.EnabledFolders.All(parent.EnabledFolders.Contains)))
            && (parent.MaxParentalRating is null || (child.MaxParentalRating is not null && child.MaxParentalRating <= parent.MaxParentalRating))
            && (parent.MaxParentalSubRating is null || (child.MaxParentalSubRating is not null && child.MaxParentalSubRating <= parent.MaxParentalSubRating))
            && parent.BlockUnratedItems.All(child.BlockUnratedItems.Contains)
            && parent.BlockedTags.All(tag => child.BlockedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            && (parent.AllowedTags.Length == 0 || (child.AllowedTags.Length > 0 && child.AllowedTags.All(tag => parent.AllowedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))))
            && (parent.BlockedMediaFolders ?? []).All(folder => (child.BlockedMediaFolders ?? []).Contains(folder))
            && (parent.EnableAllDevices || (!child.EnableAllDevices && child.EnabledDevices.All(parent.EnabledDevices.Contains)))
            && (parent.RemoteClientBitrateLimit <= 0 || (child.RemoteClientBitrateLimit > 0 && child.RemoteClientBitrateLimit <= parent.RemoteClientBitrateLimit))
            && (parent.MaxActiveSessions <= 0 || (child.MaxActiveSessions > 0 && child.MaxActiveSessions <= parent.MaxActiveSessions))
            && (parent.AccessSchedules.Length == 0 || parent.AccessSchedules.Select(schedule => (schedule.DayOfWeek, schedule.StartHour, schedule.EndHour)).ToHashSet().SetEquals(child.AccessSchedules.Select(schedule => (schedule.DayOfWeek, schedule.StartHour, schedule.EndHour))));
    }

    private static Guid Owner(State state, Guid caller) => state.Profiles.SingleOrDefault(profile => profile.User.Equals(caller))?.Owner ?? caller;

    private State Read()
    {
        DirectoryPath();
        var path = Path.Combine(_root, "profiles.json");
        YtDlpPlatform.RejectLink(path);
        if (!File.Exists(path))
        {
            return new State(2, []);
        }

        if (new FileInfo(path).Length > 2097152)
        {
            throw new InvalidDataException("Profile registry exceeded its safe size.");
        }

        var info = new FileInfo(path);
        if (_lastGood is not null && _lastWrite == info.LastWriteTimeUtc && _lastLength == info.Length)
        {
            return new State(2, new List<ViewingProfile>(_lastGood.Profiles));
        }

        var payload = File.ReadAllBytes(path);
        if (payload.Length > 2097152)
        {
            throw new InvalidDataException("Profile registry exceeded its safe size.");
        }

        using var reader = new StreamReader(new MemoryStream(payload), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var state = JsonSerializer.Deserialize<State>(reader.ReadToEnd());
        if (state is null || state.Version is not (1 or 2) || state.Profiles is null || state.Profiles.Count > 1000
            || state.Profiles.Any(profile => profile is null || profile.Owner.Equals(Guid.Empty) || profile.User.Equals(Guid.Empty) || profile.Owner.Equals(profile.User) || string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 40 || profile.Name.Any(char.IsControl) || profile.Preset is not ("kids" or "adult"))
            || state.Profiles.Select(profile => profile.User).Distinct().Count() != state.Profiles.Count
            || state.Profiles.Any(profile => state.Profiles.Any(other => other.Owner.Equals(profile.User))))
        {
            throw new InvalidDataException("Profile registry requires administrator recovery; preserve its files.");
        }

        if (state.Version == 1)
        {
            var snapshot = Path.Combine(_root, "profiles.v1.json");
            YtDlpPlatform.RejectLink(snapshot);
            if (File.Exists(snapshot))
            {
                if (new FileInfo(snapshot).Length > 2097152 || !File.ReadAllBytes(snapshot).AsSpan().SequenceEqual(payload))
                {
                    throw new InvalidDataException("Legacy profile snapshot differs; preserve both files for administrator review.");
                }

                Private(snapshot);
            }
            else
            {
                AtomicWrite(snapshot, payload, overwrite: false);
            }

            state = new State(2, state.Profiles.Select(profile => profile with
            {
                Lifecycle = profile.Ready ? "active" : "review",
                PendingLibraries = null
            }).ToList());
            Write(state);
            info = new FileInfo(path);
        }
        else if (state.Profiles.Any(profile => !ValidLifecycle(profile)))
        {
            throw new InvalidDataException("Profile lifecycle requires administrator recovery; preserve its files.");
        }

        _lastGood = state;
        _lastWrite = info.LastWriteTimeUtc;
        _lastLength = info.Length;
        return new State(2, new List<ViewingProfile>(state.Profiles));
    }

    private static bool ValidLifecycle(ViewingProfile profile)
    {
        if (profile.Lifecycle is not ("active" or "provisioning" or "deleting" or "review")
            || profile.Ready != (profile.Lifecycle == "active"))
        {
            return false;
        }

        return profile.Lifecycle == "provisioning"
            ? profile.PendingLibraries is { Length: >= 1 and <= 100 } folders
                && folders.Distinct().Count() == folders.Length && !folders.Contains(Guid.Empty)
            : profile.PendingLibraries is null;
    }

    private static byte[] Serialize(State state, int maximum = 2097152)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(state);
        return payload.Length <= maximum ? payload : throw new InvalidDataException("Profile registry exceeded its safe size.");
    }

    private byte[] Key(State state)
    {
        DirectoryPath();
        var path = Path.Combine(_root, "credentials.key");
        YtDlpPlatform.RejectLink(path);
        if (!File.Exists(path))
        {
            if (state.Profiles.Count != 0)
            {
                throw new InvalidDataException("Managed profile key is missing; restore configuration rather than replacing it.");
            }

            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            Private(path);
            stream.Write(RandomNumberGenerator.GetBytes(32));
            stream.Flush(flushToDisk: true);
        }

        if (new FileInfo(path).Length != 32)
        {
            throw new InvalidDataException("Managed profile key requires recovery.");
        }

        Private(path);
        return File.ReadAllBytes(path);
    }

    private static string Secret(byte[] key, ViewingProfile profile) => Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("CastlumoraViewingProfile/v1/" + profile.Owner.ToString("N") + "/" + profile.User.ToString("N"))));

    private void DirectoryPath()
    {
        for (var path = _root; path is not null; path = Path.GetDirectoryName(path))
        {
            YtDlpPlatform.RejectLink(path);
        }

        YtDlpPlatform.PrivateDirectory(_root);
    }

    private void Write(State state)
    {
        AtomicWrite(Path.Combine(_root, "profiles.json"), Serialize(state), overwrite: true);
        _lastWrite = DateTime.MinValue;
    }

    private void AtomicWrite(string path, byte[] payload, bool overwrite)
    {
        YtDlpPlatform.RejectLink(path);
        var temporary = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                Private(temporary);
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static void Private(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private sealed record ViewingProfile(Guid Owner, Guid User, string Name, string Preset, bool Ready, string? Lifecycle = null, Guid[]? PendingLibraries = null);

    private sealed record State(int Version, List<ViewingProfile> Profiles);
}
