﻿namespace Negwork.WebApi.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Http;
    using AutoMapper;
    using Data.Models;
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using Microsoft.AspNet.Identity.Owin;
    using Microsoft.Owin.Security;
    using Microsoft.Owin.Security.Cookies;
    using Microsoft.Owin.Security.OAuth;
    using Models;
    using Models.UserModels;
    using Providers;
    using Results;
    using Services.Common;
    using Services.Data.Contracts;

    [Authorize]
    [RoutePrefix("api/Users")]
    public class AccountController : ApiController
    {
        private const string LocalLoginProvider = "Local";
        private ApplicationUserManager _userManager;
        private IUsersService data;

        public AccountController(IUsersService data)
        {
            this.data = data;
        }

        public AccountController(
            ApplicationUserManager userManager,
            ISecureDataFormat<AuthenticationTicket> accessTokenFormat)
        {
            this.UserManager = userManager;
            this.AccessTokenFormat = accessTokenFormat;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return this._userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }

            private set
            {
                this._userManager = value;
            }
        }

        public ISecureDataFormat<AuthenticationTicket> AccessTokenFormat { get; private set; }

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        // GET api/Account/UserInfo
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("UserInfo")]
        public UserInfoViewModel GetUserInfo()
        {
            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            return new UserInfoViewModel
            {
                Email = User.Identity.GetUserName(),
                HasRegistered = externalLogin == null,
                LoginProvider = externalLogin != null ? externalLogin.LoginProvider : null
            };
        }

        // POST api/Account/Logout
        [Route("Logout")]
        public IHttpActionResult Logout()
        {
            this.Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return this.Ok();
        }

        // GET api/Account/ManageInfo?returnUrl=%2F&generateState=true
        [Route("ManageInfo")]
        public async Task<ManageInfoViewModel> GetManageInfo(string returnUrl, bool generateState = false)
        {
            IdentityUser user = await this.UserManager.FindByIdAsync(User.Identity.GetUserId());

            if (user == null)
            {
                return null;
            }

            List<UserLoginInfoViewModel> logins = new List<UserLoginInfoViewModel>();

            foreach (IdentityUserLogin linkedAccount in user.Logins)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = linkedAccount.LoginProvider,
                    ProviderKey = linkedAccount.ProviderKey
                });
            }

            if (user.PasswordHash != null)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = LocalLoginProvider,
                    ProviderKey = user.UserName,
                });
            }

            return new ManageInfoViewModel
            {
                LocalLoginProvider = LocalLoginProvider,
                Email = user.UserName,
                Logins = logins,
                ExternalLoginProviders = this.GetExternalLogins(returnUrl, generateState)
            };
        }

        // POST api/Account/ChangePassword
        [Route("ChangePassword")]
        public async Task<IHttpActionResult> ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            IdentityResult result = await this.UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            return this.Ok();
        }

        // POST api/Account/SetPassword
        [Route("SetPassword")]
        public async Task<IHttpActionResult> SetPassword(SetPasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            IdentityResult result = await this.UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);

            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            return this.Ok();
        }

        // POST api/Account/AddExternalLogin
        [Route("AddExternalLogin")]
        public async Task<IHttpActionResult> AddExternalLogin(AddExternalLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            this.Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);

            AuthenticationTicket ticket = this.AccessTokenFormat.Unprotect(model.ExternalAccessToken);

            if (ticket == null || ticket.Identity == null || (ticket.Properties != null
                && ticket.Properties.ExpiresUtc.HasValue
                && ticket.Properties.ExpiresUtc.Value < DateTimeOffset.UtcNow))
            {
                return this.BadRequest("External login failure.");
            }

            ExternalLoginData externalData = ExternalLoginData.FromIdentity(ticket.Identity);

            if (externalData == null)
            {
                return this.BadRequest("The external login is already associated with an account.");
            }

            IdentityResult result = await this.UserManager.AddLoginAsync(
                User.Identity.GetUserId(),
                new UserLoginInfo(externalData.LoginProvider, externalData.ProviderKey));

            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            return this.Ok();
        }

        // POST api/Account/RemoveLogin
        [Route("RemoveLogin")]
        public async Task<IHttpActionResult> RemoveLogin(RemoveLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            IdentityResult result;

            if (model.LoginProvider == LocalLoginProvider)
            {
                result = await this.UserManager.RemovePasswordAsync(User.Identity.GetUserId());
            }
            else
            {
                result = await this.UserManager.RemoveLoginAsync(
                    User.Identity.GetUserId(),
                    new UserLoginInfo(model.LoginProvider, model.ProviderKey));
            }

            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            return this.Ok();
        }

        // GET api/Account/ExternalLogin
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalCookie)]
        [AllowAnonymous]
        [Route("ExternalLogin", Name = "ExternalLogin")]
        public async Task<IHttpActionResult> GetExternalLogin(string provider, string error = null)
        {
            if (error != null)
            {
                return this.Redirect(Url.Content("~/") + "#error=" + Uri.EscapeDataString(error));
            }

            if (!User.Identity.IsAuthenticated)
            {
                return new ChallengeResult(provider, this);
            }

            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            if (externalLogin == null)
            {
                return this.InternalServerError();
            }

            if (externalLogin.LoginProvider != provider)
            {
                this.Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                return new ChallengeResult(provider, this);
            }

            User user = await this.UserManager.FindAsync(new UserLoginInfo(
                externalLogin.LoginProvider,
                externalLogin.ProviderKey));

            bool hasRegistered = user != null;

            if (hasRegistered)
            {
                this.Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);

                ClaimsIdentity oAuthIdentity = await user.GenerateUserIdentityAsync(
                    this.UserManager,
                   OAuthDefaults.AuthenticationType);
                ClaimsIdentity cookieIdentity = await user.GenerateUserIdentityAsync(
                    this.UserManager,
                    CookieAuthenticationDefaults.AuthenticationType);

                AuthenticationProperties properties = ApplicationOAuthProvider.CreateProperties(user.UserName);
                this.Authentication.SignIn(properties, oAuthIdentity, cookieIdentity);
            }
            else
            {
                IEnumerable<Claim> claims = externalLogin.GetClaims();
                ClaimsIdentity identity = new ClaimsIdentity(claims, OAuthDefaults.AuthenticationType);
                this.Authentication.SignIn(identity);
            }

            return this.Ok();
        }

        // GET api/Account/ExternalLogins?returnUrl=%2F&generateState=true
        [AllowAnonymous]
        [Route("ExternalLogins")]
        public IEnumerable<ExternalLoginViewModel> GetExternalLogins(string returnUrl, bool generateState = false)
        {
            IEnumerable<AuthenticationDescription> descriptions = this.Authentication.GetExternalAuthenticationTypes();
            List<ExternalLoginViewModel> logins = new List<ExternalLoginViewModel>();

            string state;

            if (generateState)
            {
                const int StrengthInBits = 256;
                state = RandomOAuthStateGenerator.Generate(StrengthInBits);
            }
            else
            {
                state = null;
            }

            foreach (AuthenticationDescription description in descriptions)
            {
                ExternalLoginViewModel login = new ExternalLoginViewModel
                {
                    Name = description.Caption,
                    Url = Url.Route(
                        "ExternalLogin",
                        new
                        {
                            provider = description.AuthenticationType,
                            response_type = "token",
                            client_id = Startup.PublicClientId,
                            redirect_uri = new Uri(Request.RequestUri, returnUrl).AbsoluteUri,
                            state = state
                        }),
                    State = state
                };
                logins.Add(login);
            }

            return logins;
        }

        // POST api/Account/Register
        [AllowAnonymous]
        [Route("Register")]
        public async Task<IHttpActionResult> Register(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var user = new User()
            {
                UserName = model.UserName,
                Email = model.Email,
                DateOfBirth = model.DateOfBirth,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Gender = model.Gender,
                AdditionalInfo = model.AdditionalInfo
            };

            IdentityResult result = await this.UserManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            return this.Ok();
        }

        [HttpGet]
        [Authorize]
        [Route("Identity")]
        public IHttpActionResult Identity()
        {
            var userId = this.User.Identity.GetUserId();
            var user = this.data.GetById(userId);
            
            if (user == null)
            {
                return this.InternalServerError();
            }

            var userInfo = Mapper.Map<UserIdentityResponseModel>(user);

            return this.Json(userInfo);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("Profile/{username}")]
        public IHttpActionResult GetUserProfile(string username)
        {
            var user = this.data.GetByName(username);

            if (user == null)
            {
                return this.BadRequest("There is no such user.");
            }

            var userInfo = Mapper.Map<UserInfoResponseModel>(user);

            return this.Ok(userInfo);
        }

        [HttpPut]
        [Authorize]
        [Route("Settings")]
        public IHttpActionResult UpdateUser(UserUpdateRequestModel model)
        {
            if (model == null)
            {
                return this.BadRequest("There was a problem.");
            }

            var userName = this.User.Identity.GetUserName();

            if (userName != model.UserName)
            {
                return this.BadRequest("You are not authorized to edit this profile.");
            }

            if (!this.ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var userId = this.User.Identity.GetUserId();

            var result = this.data.UpdateInfo(userId, model.FirstName, model.LastName, model.AdditionalInfo, model.ProfileImage);

            if (result == ServiceResponse.NotFound)
            {
                return this.InternalServerError();
            }

            return this.Ok();
        }

        // POST api/Account/RegisterExternal
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("RegisterExternal")]
        public async Task<IHttpActionResult> RegisterExternal(RegisterExternalBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return this.BadRequest(this.ModelState);
            }

            var info = await this.Authentication.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return this.InternalServerError();
            }

            var user = new User() { UserName = model.Email, Email = model.Email };

            IdentityResult result = await this.UserManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            result = await this.UserManager.AddLoginAsync(user.Id, info.Login);
            if (!result.Succeeded)
            {
                return this.GetErrorResult(result);
            }

            return this.Ok();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this._userManager != null)
            {
                this._userManager.Dispose();
                this._userManager = null;
            }

            base.Dispose(disposing);
        }

        #region Helpers

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return this.InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return this.BadRequest();
                }

                return this.BadRequest(this.ModelState);
            }

            return null;
        }

        private static class RandomOAuthStateGenerator
        {
            private static RandomNumberGenerator _random = new RNGCryptoServiceProvider();

            public static string Generate(int strengthInBits)
            {
                const int BitsPerByte = 8;

                if (strengthInBits % BitsPerByte != 0)
                {
                    throw new ArgumentException("strengthInBits must be evenly divisible by 8.", "strengthInBits");
                }

                int strengthInBytes = strengthInBits / BitsPerByte;

                byte[] data = new byte[strengthInBytes];
                _random.GetBytes(data);
                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }

            public string ProviderKey { get; set; }

            public string UserName { get; set; }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || string.IsNullOrEmpty(providerKeyClaim.Issuer)
                    || string.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirstValue(ClaimTypes.Name)
                };
            }

            public IList<Claim> GetClaims()
            {
                IList<Claim> claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.NameIdentifier, this.ProviderKey, null, this.LoginProvider));

                if (this.UserName != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, this.UserName, null, this.LoginProvider));
                }

                return claims;
            }
        }

        #endregion
    }
}
