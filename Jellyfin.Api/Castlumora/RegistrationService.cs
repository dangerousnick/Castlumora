using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Extensions.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Users;

namespace Jellyfin.Api.Castlumora;

/// <summary>Administrator-controlled registration with hashed invitations and durable use reservations.</summary>
public sealed class RegistrationService : IDisposable
{
    private readonly string _path;
    private readonly IUserManager _users;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _attemptLock = new();
    private readonly Dictionary<string, (DateTimeOffset Start, int Count)> _attempts = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="RegistrationService"/> class.</summary>
    /// <param name="paths">Server configuration paths.</param>
    /// <param name="users">Existing account/password/policy implementation.</param>
    /// <param name="clock">Optional test clock.</param>
    public RegistrationService(IApplicationPaths paths, IUserManager users, TimeProvider? clock = null)
    {
        _path = Path.Combine(paths.ConfigurationDirectoryPath, "castlumora", "registration.json");
        _users = users;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Consumes an anonymous signup attempt, bounded to five per minute per normalized address.</summary>
    /// <param name="address">Normalized request address; retained only in memory for the window.</param>
    /// <returns>Whether the attempt is allowed.</returns>
    public bool TryAttempt(string address)
    {
        lock (_attemptLock)
        {
            var now = _clock.GetUtcNow();
            foreach (var stale in _attempts.Where(entry => now - entry.Value.Start >= TimeSpan.FromMinutes(1)).Select(entry => entry.Key).ToArray())
            {
                _attempts.Remove(stale);
            }

            if (_attempts.TryGetValue(address, out var window))
            {
                if (window.Count >= 5)
                {
                    return false;
                }

                _attempts[address] = (window.Start, window.Count + 1);
                return true;
            }

            if (_attempts.Count >= 4096)
            {
                return false;
            }

            _attempts[address] = (now, 1);
            return true;
        }
    }

    /// <summary>Gets public registration availability without invitation or policy details.</summary>
    /// <returns>Public flags.</returns>
    public async Task<JsonObject> PublicAsync()
    {
        var state = await AdminAsync().ConfigureAwait(false);
        return new JsonObject { ["enabled"] = state["settings"]!["enabled"]!.GetValue<bool>(), ["requireInvitation"] = state["settings"]!["requireInvitation"]!.GetValue<bool>() };
    }

    /// <summary>Gets administrator settings/invitation metadata/audit without token hashes.</summary>
    /// <returns>Administrator state.</returns>
    public async Task<JsonObject> AdminAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await ReadAsync().ConfigureAwait(false);
            foreach (var invite in state["invitations"]!.AsArray())
            {
                invite!.AsObject().Remove("tokenHash");
            }

            return state;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Saves registration switches and non-administrator default policy with revision protection.</summary>
    /// <param name="value">Settings including current revision.</param>
    /// <returns>Saved settings.</returns>
    public async Task<JsonObject> SaveSettingsAsync(JsonObject value)
    {
        var policy = Policy(value["defaultPolicy"]);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await ReadAsync().ConfigureAwait(false);
            var current = state["settings"]!;
            if (value["revision"]?.GetValue<int>() != current["revision"]!.GetValue<int>())
            {
                throw new InvalidOperationException("Registration settings changed; reload first.");
            }

            var settings = new JsonObject
            {
                ["revision"] = current["revision"]!.GetValue<int>() + 1,
                ["enabled"] = value["enabled"]!.GetValue<bool>(),
                ["requireInvitation"] = value["requireInvitation"]!.GetValue<bool>(),
                ["defaultPolicy"] = JsonSerializer.SerializeToNode(policy, JsonDefaults.Options)
            };
            state["settings"] = settings;
            await WriteAsync(state).ConfigureAwait(false);
            return settings.DeepClone().AsObject();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Creates an expiring invitation; the raw token is returned once and never persisted.</summary>
    /// <param name="value">Name, expiresAt, maxUses and optional policy.</param>
    /// <param name="actor">Administrator identity.</param>
    /// <returns>Invitation ID and one-time token.</returns>
    public async Task<JsonObject> CreateInviteAsync(JsonObject value, Guid actor)
    {
        var label = value["name"]?.GetValue<string>()?.Trim();
        var uses = value["maxUses"]?.GetValue<int>() ?? 1;
        if (string.IsNullOrEmpty(label) || label.Length > 120 || uses < 1 || !DateTimeOffset.TryParse(value["expiresAt"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) || expires <= _clock.GetUtcNow())
        {
            throw new ArgumentException("A label, future expiry and positive successful-registration cap are required.");
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await ReadAsync().ConfigureAwait(false);
            var policy = Policy(value["policy"] ?? state["settings"]!["defaultPolicy"]);
            var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var id = Guid.NewGuid().ToString("N");
            state["invitations"]!.AsArray().Add(new JsonObject
            {
                ["id"] = id,
                ["name"] = label,
                ["tokenHash"] = Hash(token),
                ["createdBy"] = actor.ToString("N"),
                ["createdAt"] = _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                ["expiresAt"] = expires.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                ["maxUses"] = uses,
                ["successfulUses"] = 0,
                ["reservedUses"] = 0,
                ["revoked"] = false,
                ["policy"] = JsonSerializer.SerializeToNode(policy, JsonDefaults.Options)
            });
            await WriteAsync(state).ConfigureAwait(false);
            return new JsonObject { ["id"] = id, ["token"] = token };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Revokes future use of an administrator-created invitation.</summary>
    /// <param name="id">Invitation ID.</param>
    /// <returns>A task.</returns>
    public async Task RevokeAsync(string id)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await ReadAsync().ConfigureAwait(false);
            var invite = state["invitations"]!.AsArray().FirstOrDefault(entry => entry!["id"]!.GetValue<string>() == id) ?? throw new KeyNotFoundException("Invitation not found.");
            invite["revoked"] = true;
            await WriteAsync(state).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Provisions a disabled account, password and policy before durable invitation consumption and activation.</summary>
    /// <param name="name">New username.</param>
    /// <param name="password">Password, never persisted in the registration journal.</param>
    /// <param name="token">Optional raw invitation from the request body.</param>
    /// <returns>The new account ID; no authentication token is issued.</returns>
    public async Task<Guid> RegisterAsync(string name, string password, string? token)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 128 || password.Length is < 8 or > 1024 || token?.Length > 256)
        {
            throw new ArgumentException("Invalid registration details.");
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await ReadAsync().ConfigureAwait(false);
            var settings = state["settings"]!;
            if (!settings["enabled"]!.GetValue<bool>())
            {
                throw new InvalidOperationException("Registration is closed.");
            }

            JsonNode? invite = null;
            if (!string.IsNullOrEmpty(token))
            {
                var hash = Convert.FromHexString(Hash(token));
                invite = state["invitations"]!.AsArray().FirstOrDefault(entry => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(entry!["tokenHash"]!.GetValue<string>()), hash));
                if (invite is null || invite["revoked"]!.GetValue<bool>() || DateTimeOffset.Parse(invite["expiresAt"]!.GetValue<string>(), CultureInfo.InvariantCulture) <= _clock.GetUtcNow() || (long)invite["successfulUses"]!.GetValue<int>() + invite["reservedUses"]!.GetValue<int>() >= invite["maxUses"]!.GetValue<int>())
                {
                    throw new InvalidOperationException("Registration is unavailable with these details.");
                }
            }
            else if (settings["requireInvitation"]!.GetValue<bool>())
            {
                throw new InvalidOperationException("An invitation is required.");
            }

            var policy = Policy(invite?["policy"] ?? settings["defaultPolicy"]);
            var finalDisabled = policy.IsDisabled;
            policy.IsDisabled = true;
            var audit = new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["invitationId"] = invite?["id"]?.DeepClone(),
                ["username"] = name,
                ["at"] = _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                ["status"] = "reserved"
            };
            state["registrations"]!.AsArray().Add(audit);
            if (invite is not null)
            {
                invite["reservedUses"] = invite["reservedUses"]!.GetValue<int>() + 1;
            }

            // Reservations survive a process crash and cannot silently permit extra signups.
            await WriteAsync(state).ConfigureAwait(false);
            User? created = null;
            var consumed = false;
            var consumptionPrepared = false;
            try
            {
                created = await _users.CreateUserAsync(name, true).ConfigureAwait(false);
                audit["userId"] = created.Id.ToString("N");
                audit["status"] = "provisioning";
                await WriteAsync(state).ConfigureAwait(false);
                await _users.ChangePassword(created.Id, password).ConfigureAwait(false);
                // Signup uses the actual providers chosen by the existing account manager, never request-supplied providers.
                policy.AuthenticationProviderId = created.AuthenticationProviderId;
                policy.PasswordResetProviderId = created.PasswordResetProviderId;
                BindSchedules(policy, created.Id);
                await _users.UpdatePolicyAsync(created.Id, policy).ConfigureAwait(false);
                audit["status"] = "completed";
                if (invite is not null)
                {
                    invite["reservedUses"] = invite["reservedUses"]!.GetValue<int>() - 1;
                    invite["successfulUses"] = invite["successfulUses"]!.GetValue<int>() + 1;
                }

                consumptionPrepared = true;
                await WriteAsync(state).ConfigureAwait(false);
                consumed = true;
                policy.IsDisabled = finalDisabled;
                BindSchedules(policy, created.Id);
                await _users.UpdatePolicyAsync(created.Id, policy).ConfigureAwait(false);
                return created.Id;
            }
            catch
            {
                if (!consumed)
                {
                    if (consumptionPrepared && invite is not null)
                    {
                        invite["successfulUses"] = Math.Max(0, invite["successfulUses"]!.GetValue<int>() - 1);
                        invite["reservedUses"] = invite["reservedUses"]!.GetValue<int>() + 1;
                    }

                    var removed = created is null;
                    if (created is not null)
                    {
                        try
                        {
                            await _users.DeleteUserAsync(created.Id).ConfigureAwait(false);
                            removed = true;
                        }
                        catch (Exception)
                        {
                            // The account remains disabled. An administrator must resolve its reservation after cleanup.
                            audit["status"] = "cleanupRequired";
                        }
                    }

                    if (removed)
                    {
                        audit["status"] = "failed";
                        if (invite is not null)
                        {
                            invite["reservedUses"] = Math.Max(0, invite["reservedUses"]!.GetValue<int>() - 1);
                        }
                    }

                    await WriteAsync(state).ConfigureAwait(false);
                }

                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Resolves a crash/cleanup reservation without automatically enabling any account.</summary>
    /// <param name="id">Registration journal ID.</param>
    /// <param name="consume">Count the account as completed, or release only if no matching account exists.</param>
    /// <param name="actor">Administrator identity.</param>
    /// <returns>A task.</returns>
    public async Task ResolveAsync(string id, bool consume, Guid actor)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var state = await ReadAsync().ConfigureAwait(false);
            var audit = state["registrations"]!.AsArray().FirstOrDefault(entry => entry!["id"]!.GetValue<string>() == id) ?? throw new KeyNotFoundException("Registration not found.");
            if (audit["status"]!.GetValue<string>() is not ("reserved" or "provisioning" or "cleanupRequired"))
            {
                throw new InvalidOperationException("This registration has no outstanding reservation.");
            }

            var account = _users.GetUserByName(audit["username"]!.GetValue<string>());
            if ((consume && account is null) || (!consume && account is not null))
            {
                throw new InvalidOperationException("Inspect the matching account before counting or releasing its reservation.");
            }

            var invite = state["invitations"]!.AsArray().FirstOrDefault(entry => entry!["id"]!.GetValue<string>() == audit["invitationId"]?.GetValue<string>());
            if (invite is not null)
            {
                invite["reservedUses"] = Math.Max(0, invite["reservedUses"]!.GetValue<int>() - 1);
                if (consume)
                {
                    invite["successfulUses"] = invite["successfulUses"]!.GetValue<int>() + 1;
                }
            }

            audit["status"] = consume ? "completed" : "failed";
            audit["resolvedBy"] = actor.ToString("N");
            await WriteAsync(state).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private static UserPolicy Policy(JsonNode? value)
    {
        var policy = value is null ? DefaultsPolicy() : value.Deserialize<UserPolicy>(JsonDefaults.Options) ?? throw new ArgumentException("Missing policy.");
        if (policy.IsAdministrator)
        {
            throw new ArgumentException("Registration policies cannot create administrators.");
        }

        policy.InvalidLoginAttemptCount = 0;
        policy.AuthenticationProviderId = null!;
        policy.PasswordResetProviderId = null!;
        return policy;
    }

    private static void BindSchedules(UserPolicy policy, Guid userId)
        => policy.AccessSchedules = (policy.AccessSchedules ?? []).Select(schedule => new AccessSchedule(schedule.DayOfWeek, schedule.StartHour, schedule.EndHour, userId)).ToArray();

    private static UserPolicy DefaultsPolicy() => new()
    {
        IsAdministrator = false,
        EnableAllFolders = false,
        EnabledFolders = [],
        EnableAllChannels = false,
        EnabledChannels = [],
        EnableLiveTvAccess = false,
        EnableLiveTvManagement = false,
        EnableContentDownloading = false,
        EnablePublicSharing = false,
        EnableSharedDeviceControl = false,
        EnableRemoteControlOfOtherUsers = false
    };

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<JsonObject> ReadAsync()
    {
        if (File.Exists(_path))
        {
            return JsonNode.Parse(await File.ReadAllTextAsync(_path).ConfigureAwait(false))!.AsObject();
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["settings"] = new JsonObject { ["revision"] = 0, ["enabled"] = false, ["requireInvitation"] = true, ["defaultPolicy"] = JsonSerializer.SerializeToNode(DefaultsPolicy(), JsonDefaults.Options) },
            ["invitations"] = new JsonArray(),
            ["registrations"] = new JsonArray()
        };
    }

    private async Task WriteAsync(JsonObject state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, state.ToJsonString()).ConfigureAwait(false);
            File.Move(temporary, _path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
