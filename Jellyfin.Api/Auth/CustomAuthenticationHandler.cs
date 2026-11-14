using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Jellyfin.Api.Castlumora;
using Jellyfin.Api.Constants;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using MediaBrowser.Controller.Authentication;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Api.Auth
{
    /// <summary>
    /// Custom authentication handler wrapping the legacy authentication.
    /// </summary>
    public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IAuthService _authService;
        private readonly ProfileService? _profiles;
        private readonly ILogger<CustomAuthenticationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomAuthenticationHandler" /> class.
        /// </summary>
        /// <param name="authService">The jellyfin authentication service.</param>
        /// <param name="options">Options monitor.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="encoder">The url encoder.</param>
        public CustomAuthenticationHandler(
            IAuthService authService,
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : this(authService, options, logger, encoder, null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="CustomAuthenticationHandler"/> class with managed-profile owner checks.</summary>
        /// <param name="authService">Inherited authentication.</param>
        /// <param name="options">Scheme options.</param>
        /// <param name="logger">Logger factory.</param>
        /// <param name="encoder">URL encoder.</param>
        /// <param name="profiles">Managed-profile policy; null preserves the original constructor contract.</param>
        public CustomAuthenticationHandler(
            IAuthService authService,
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ProfileService? profiles)
            : base(options, logger, encoder)
        {
            _profiles = profiles;
            _authService = authService;
            _logger = logger.CreateLogger<CustomAuthenticationHandler>();
        }

        /// <inheritdoc />
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                var authorizationInfo = await _authService.Authenticate(Request).ConfigureAwait(false);
                if (!authorizationInfo.HasToken)
                {
                    return AuthenticateResult.NoResult();
                }

                if (!authorizationInfo.IsApiKey && _profiles is not null
                    && !await _profiles.SessionAllowedAsync(authorizationInfo.UserId, Context.RequestAborted).ConfigureAwait(false))
                {
                    return AuthenticateResult.Fail("Managed viewing profile requires administrator review.");
                }

                var role = UserRoles.User;
                if (authorizationInfo.IsApiKey
                    || (authorizationInfo.User?.HasPermission(PermissionKind.IsAdministrator) ?? false))
                {
                    role = UserRoles.Administrator;
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, authorizationInfo.User?.Username ?? string.Empty),
                    new Claim(ClaimTypes.Role, role),
                    new Claim(InternalClaimTypes.UserId, authorizationInfo.UserId.ToString("N", CultureInfo.InvariantCulture)),
                    new Claim(InternalClaimTypes.DeviceId, authorizationInfo.DeviceId ?? string.Empty),
                    new Claim(InternalClaimTypes.Device, authorizationInfo.Device ?? string.Empty),
                    new Claim(InternalClaimTypes.Client, authorizationInfo.Client ?? string.Empty),
                    new Claim(InternalClaimTypes.Version, authorizationInfo.Version ?? string.Empty),
                    new Claim(InternalClaimTypes.Token, authorizationInfo.Token),
                    new Claim(InternalClaimTypes.IsApiKey, authorizationInfo.IsApiKey.ToString(CultureInfo.InvariantCulture))
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch (AuthenticationException ex)
            {
                _logger.LogDebug(ex, "Error authenticating with {Handler}", nameof(CustomAuthenticationHandler));
                return AuthenticateResult.NoResult();
            }
            catch (SecurityException ex)
            {
                return AuthenticateResult.Fail(ex);
            }
        }
    }
}
