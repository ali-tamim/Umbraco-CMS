﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Migrations.Install;
using Umbraco.Core.Services;
using Umbraco.Web.Install.Models;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Web.Security;

namespace Umbraco.Web.Install.InstallSteps
{
    /// <summary>
    /// This is the first UI step for a brand new install
    /// </summary>
    /// <remarks>
    /// By default this will show the user view which is the most basic information to configure a new install, but if an install get's interrupted because of an
    /// error, etc... and the end-user refreshes the installer then we cannot show the user screen because they've already entered that information so instead we'll
    /// display a simple continue installation view.
    /// </remarks>
    [InstallSetupStep(InstallationType.NewInstall, "User", 20, "")]
    internal class NewInstallStep : InstallSetupStep<UserModel>
    {
        private readonly IUserService _userService;
        private readonly DatabaseBuilder _databaseBuilder;
        private static HttpClient _httpClient;
        private readonly IGlobalSettings _globalSettings;
        private readonly IUserPasswordConfiguration _passwordConfiguration;
        private readonly ISecuritySettings _securitySettings;
        private readonly IConnectionStrings _connectionStrings;
        private readonly ICookieManager _cookieManager;

        public NewInstallStep(IUserService userService, DatabaseBuilder databaseBuilder, IGlobalSettings globalSettings, IUserPasswordConfiguration passwordConfiguration, ISecuritySettings securitySettings, IConnectionStrings connectionStrings, ICookieManager cookieManager)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _databaseBuilder = databaseBuilder ?? throw new ArgumentNullException(nameof(databaseBuilder));
            _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
            _passwordConfiguration = passwordConfiguration ?? throw new ArgumentNullException(nameof(passwordConfiguration));
            _securitySettings = securitySettings ?? throw new ArgumentNullException(nameof(securitySettings));
            _connectionStrings = connectionStrings ?? throw new ArgumentNullException(nameof(connectionStrings));
            _cookieManager = cookieManager;
        }

        public override async Task<InstallSetupResult> ExecuteAsync(UserModel user)
        {
            var admin = _userService.GetUserById(Constants.Security.SuperUserId);
            if (admin == null)
            {
                throw new InvalidOperationException("Could not find the super user!");
            }
            admin.Email = user.Email.Trim();
            admin.Name = user.Name.Trim();
            admin.Username = user.Email.Trim();

            _userService.Save(admin);

            //TODO: This needs to be reintroduced, when members are compatible with ASP.NET Core Identity.
            // var userManager = _httpContextAccessor.GetRequiredHttpContext().GetOwinContext().GetBackOfficeUserManager();
            // var membershipUser = await userManager.FindByIdAsync(Constants.Security.SuperUserId.ToString());
            // if (membershipUser == null)
            // {
            //     throw new InvalidOperationException(
            //         $"No user found in membership provider with id of {Constants.Security.SuperUserId}.");
            // }
            //
            // //To change the password here we actually need to reset it since we don't have an old one to use to change
            // var resetToken = await userManager.GeneratePasswordResetTokenAsync(membershipUser);
            // if (string.IsNullOrWhiteSpace(resetToken))
            //     throw new InvalidOperationException("Could not reset password: unable to generate internal reset token");
            //
            // var resetResult = await userManager.ChangePasswordWithResetAsync(membershipUser.Id, resetToken, user.Password.Trim());
            // if (!resetResult.Succeeded)
            //     throw new InvalidOperationException("Could not reset password: " + string.Join(", ", resetResult.Errors.ToErrorMessage()));




            if (user.SubscribeToNewsLetter)
            {
                if (_httpClient == null)
                    _httpClient = new HttpClient();

                var values = new NameValueCollection { { "name", admin.Name }, { "email", admin.Email } };
                var content = new StringContent(JsonConvert.SerializeObject(values), Encoding.UTF8, "application/json");

                try
                {
                    var response = _httpClient.PostAsync("https://shop.umbraco.com/base/Ecom/SubmitEmail/installer.aspx", content).Result;
                }
                catch { /* fail in silence */ }
            }

            return null;
        }

        /// <summary>
        /// Return a custom view model for this step
        /// </summary>
        public override object ViewModel
        {
            get
            {
                return new
                {
                    minCharLength = _passwordConfiguration.RequiredLength,
                    minNonAlphaNumericLength = _passwordConfiguration.RequireNonLetterOrDigit ? 1 : 0
                };
            }
        }

        public override string View
        {
            get
            {
                return RequiresExecution(null)
              //the user UI
                ? "user"
              //the continue install UI
              : "continueinstall";
            }
        }

        public override bool RequiresExecution(UserModel model)
        {
            //now we have to check if this is really a new install, the db might be configured and might contain data
            var databaseSettings = _connectionStrings[Constants.System.UmbracoConnectionName];
            if (databaseSettings.IsConnectionStringConfigured() && _databaseBuilder.IsDatabaseConfigured)
                return _databaseBuilder.HasSomeNonDefaultUser() == false;

            // In this one case when it's a brand new install and nothing has been configured, make sure the
            // back office cookie is cleared so there's no old cookies lying around causing problems
            _cookieManager.ExpireCookie(_securitySettings.AuthCookieName);

                return true;
        }
    }
}
