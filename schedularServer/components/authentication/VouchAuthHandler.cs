using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace components.authentication
{
    public class VouchOptions: AuthenticationSchemeOptions { };

    class AuthConfig
    {
        public bool enabled { get; set; }

        public string[] users { get; set; }
        public AuthConfig()
        {
            users = new string[] { };
        }
    }

    public class VouchAuthHandler : AuthenticationHandler<VouchOptions>
    {
        readonly ILogger _logger;
        readonly AuthConfig _config = new AuthConfig();

        public VouchAuthHandler(
            IConfiguration configuration,
            ILogger<VouchAuthHandler> logger,
            IOptionsMonitor<VouchOptions> options,
            ILoggerFactory loggerFactory,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, loggerFactory, encoder, clock)
        {
            _logger = logger;
            configuration.GetSection("authentication").Bind(_config);
        }

        readonly static string VOUCHHEADER = @"X-Vouch-User";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                var userid = "guest";
                if (_config.enabled)
                {
                    if (!Request.Headers.ContainsKey(VOUCHHEADER))
                        throw new Exception($"No VOUCHHEADER -> {VOUCHHEADER}");

                    userid = Request.Headers[VOUCHHEADER].ToString();
                    if (string.IsNullOrWhiteSpace(userid) || !_config.users.Contains(userid))
                    {
                        throw new Exception($"user {userid} not recognized");
                    }
                }


                var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userid) }, Scheme.Name);

                
                var ticket = new AuthenticationTicket(new GenericPrincipal(identity, null), Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "HandleAuthenticateAsync failed");
                return Task.FromResult(AuthenticateResult.Fail("Unauthorized"));
            }
            
                
        }
    }
}
