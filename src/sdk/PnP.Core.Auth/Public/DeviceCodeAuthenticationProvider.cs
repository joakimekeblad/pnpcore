﻿using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using PnP.Core.Auth.Services.Builder.Configuration;
using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PnP.Core.Auth
{
    /// <summary>
    /// Authentication Provider that uses a device code flow for authentication
    /// </summary>
    public class DeviceCodeAuthenticationProvider : OAuthAuthenticationProvider
    {
        /// <summary>
        /// The Redirect URI for the authentication flow
        /// </summary>
        public Uri RedirectUri { get; set; }

        /// <summary>
        /// Action to notify the end user about the device code request
        /// </summary>
        public Action<DeviceCodeNotification> DeviceCodeVerification { get; set; }

        // Instance private member, to keep the token cache at service instance level
        private IPublicClientApplication publicClientApplication;

        /// <summary>
        /// Public constructor for external consumers of the library
        /// </summary>
        /// <param name="clientId">The Client ID for the Authentication Provider</param>
        /// <param name="tenantId">The Tenand ID for the Authentication Provider</param>
        /// <param name="redirectUri">The Redirect URI for the authentication flow</param>
        /// <param name="deviceCodeVerification">External action to manage the Device Code verification</param>
        public DeviceCodeAuthenticationProvider(string clientId, string tenantId,
            Uri redirectUri, Action<DeviceCodeNotification> deviceCodeVerification)
            : this(null)
        {
            DeviceCodeVerification = deviceCodeVerification;
            Init(new PnPCoreAuthenticationCredentialConfigurationOptions
            {
                ClientId = clientId,
                TenantId = tenantId,
                DeviceCode = new PnPCoreAuthenticationDeviceCodeOptions
                {
                    RedirectUri = redirectUri
                }
            });
        }

        /// <summary>
        /// Public constructor leveraging DI to initialize the ILogger interfafce
        /// </summary>
        /// <param name="logger">The instance of the logger service provided by DI</param>
        public DeviceCodeAuthenticationProvider(ILogger<OAuthAuthenticationProvider> logger)
            : base(logger)
        {
        }

        /// <summary>
        /// Initializes the Authentication Provider
        /// </summary>
        /// <param name="options">The options to use</param>
        internal override void Init(PnPCoreAuthenticationCredentialConfigurationOptions options)
        {
            // We need the UsernamePassword options
            if (options.DeviceCode == null)
            {
                throw new ConfigurationErrorsException(
                    PnPCoreAuthResources.DeviceCodeAuthenticationProvider_InvalidConfiguration);
            }

            // We need the RedirectUri
            if (options.DeviceCode.RedirectUri == null)
            {
                throw new ConfigurationErrorsException(PnPCoreAuthResources.DeviceCodeAuthenticationProvider_InvalidRedirectUri);
            }

            ClientId = !string.IsNullOrEmpty(options.ClientId) ? options.ClientId : AuthGlobals.DefaultClientId;
            TenantId = !string.IsNullOrEmpty(options.TenantId) ? options.TenantId : AuthGlobals.OrganizationsTenantId;
            RedirectUri = options.DeviceCode.RedirectUri;

            // Define the authority for the current security context
            var authority = new Uri(String.Format(System.Globalization.CultureInfo.InvariantCulture,
                AuthGlobals.AuthorityFormat,
                TenantId));

            // Build the MSAL client
            publicClientApplication = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(authority)
                .WithRedirectUri(RedirectUri.ToString())
                .Build();

            // Log the initialization information
            Log?.LogInformation(PnPCoreAuthResources.DeviceCodeAuthenticationProvider_LogInit);
        }

        /// <summary>
        /// Authenticates the specified request message.
        /// </summary>
        /// <param name="resource">Request uri</param>
        /// <param name="request">The <see cref="HttpRequestMessage"/> to authenticate.</param>
        /// <returns>The task to await.</returns>
        public override async Task AuthenticateRequestAsync(Uri resource, HttpRequestMessage request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("bearer",
                await GetAccessTokenAsync(resource).ConfigureAwait(false));
        }

        /// <summary>
        /// Gets an access token for the requested resource and scope
        /// </summary>
        /// <param name="resource">Resource to request an access token for</param>
        /// <param name="scopes">Scopes to request</param>
        /// <returns>An access token</returns>
        public override async Task<string> GetAccessTokenAsync(Uri resource, string[] scopes)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            AuthenticationResult tokenResult = null;

            var account = await publicClientApplication.GetAccountsAsync().ConfigureAwait(false);
            try
            {
                // Try to get the token from the tokens cache
                tokenResult = await publicClientApplication.AcquireTokenSilent(scopes, account.FirstOrDefault())
                    .ExecuteAsync().ConfigureAwait(false);
            }
            catch (MsalUiRequiredException)
            {
                // Try to get the token directly through AAD if it is not available in the tokens cache
                tokenResult = await publicClientApplication.AcquireTokenWithDeviceCode(scopes,
                    deviceCodeResult =>
                    {
                        DeviceCodeVerification.Invoke(new DeviceCodeNotification
                        {
                            UserCode = deviceCodeResult.UserCode,
                            Message = deviceCodeResult.Message,
                            VerificationUrl = new Uri(deviceCodeResult.VerificationUrl)
                        });
                        return Task.FromResult(0);
                    })
                    .ExecuteAsync().ConfigureAwait(false);
            }

            // Log the access token retrieval action
            Log?.LogInformation(PnPCoreAuthResources.AuthenticationProvider_LogAccessTokenRetrieval,
                GetType().Name, resource, scopes.Aggregate(string.Empty, (c, n) => c + ", " + n).TrimEnd(','));

            // Return the Access Token, if we've got it
            // In case of any exception while retrieving the access token, 
            // MSAL will throw an exception that we simply bubble up
            return tokenResult.AccessToken;
        }

        /// <summary>
        /// Gets an access token for the requested resource 
        /// </summary>
        /// <param name="resource">Resource to request an access token for</param>
        /// <returns>An access token</returns>
        public override async Task<string> GetAccessTokenAsync(Uri resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            // Use the .default scope if the scopes are not provided
            return await GetAccessTokenAsync(resource,
                new string[] { $"{resource.Scheme}://{resource.Authority}/.default" }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Provides information about the Device Code authentication request
    /// </summary>
    public class DeviceCodeNotification
    {
        /// <summary>
        /// User friendly text response that can be used for display purpose.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Verification URL where the user must navigate to authenticate using the device code and credentials
        /// </summary>
        public Uri VerificationUrl { get; set; }

        /// <summary>
        /// Device code returned by the service
        /// </summary>
        public string UserCode { get; set; }

    }
}
