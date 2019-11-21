using System.Security.Principal;

namespace Owin.Scim.Services
{
    using System.Collections.Generic;
    using System.Net;
    using System.Linq;
    using System.Threading.Tasks;

    using Configuration;

    using ErrorHandling;

    using Extensions;

    using Microsoft.FSharp.Core;

    using Model;
    using Model.Users;

    using NContext.Extensions;

    using Querying;

    using Repository;

    using Security;

    using Validation;

    public class UserService : ServiceBase, IUserService
    {
        private readonly ICanonicalizationService _CanonicalizationService;

        private readonly IManagePasswords _PasswordManager;
        private readonly IResourceValidatorFactory _ResourceValidatorFactory;
        private readonly IUserRepository _UserRepository;
        public UserService(
            ScimServerConfiguration serverConfiguration,
            IResourceVersionProvider versionProvider,
            ICanonicalizationService canonicalizationService,
            IResourceValidatorFactory resourceValidatorFactory,
            IUserRepository userRepository,
            IManagePasswords passwordManager)
            : base(serverConfiguration, versionProvider)
        {
            _CanonicalizationService = canonicalizationService;
            _UserRepository = userRepository;
            _PasswordManager = passwordManager;
            _ResourceValidatorFactory = resourceValidatorFactory;
        }

        public virtual async Task<IScimResponse<ScimUser>> CreateUser(IPrincipal principal, ScimUser user)
        {
            _CanonicalizationService.Canonicalize(user, ServerConfiguration.GetScimTypeDefinition(user.GetType()));

            var validator = await _ResourceValidatorFactory.CreateValidator(user).ConfigureAwait(false);
            var validationResult = (await validator.ValidateCreateAsync(user).ConfigureAwait(false)).ToScimValidationResult();

            if (!validationResult)
                return new ScimErrorResponse<ScimUser>(validationResult.Errors.First());

            if (user.Password != null)
                user.Password = _PasswordManager.CreateHash(user.Password);

            user.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.User);

            var userRecord = await _UserRepository.CreateUser(principal, user).ConfigureAwait(false);
            if (userRecord == null)
                return new ScimErrorResponse<ScimUser>(
                    new ScimError(
                        HttpStatusCode.BadRequest));

            // version may require the User.Id which is often generated using database unique constraints
            // therefore, we will set the version after persistence
            SetResourceVersion(userRecord);

            return new ScimDataResponse<ScimUser>(userRecord);
        }
        
        public virtual async Task<IScimResponse<Unit>> DeleteUser(IPrincipal principal, string userId)
        {
            var exists = await _UserRepository.UserExists(principal, userId).ConfigureAwait(false);
            if (!exists)
                return new ScimErrorResponse<Unit>(
                    new ScimError(
                        HttpStatusCode.NotFound,
                        detail: ScimErrorDetail.NotFound(userId)));

            await _UserRepository.DeleteUser(principal, userId).ConfigureAwait(false);

            return new ScimDataResponse<Unit>(default(Unit));
        }

        public virtual async Task<IScimResponse<IEnumerable<ScimUser>>> QueryUsers(IPrincipal principal, ScimQueryOptions options)
        {
            var users = await _UserRepository.QueryUsers(principal, options).ConfigureAwait(false) ?? new List<ScimUser>();
            users.ForEach(user =>
            {
                // repository populates meta only if it sets Created and/or LastModified
                if (user.Meta == null)
                {
                    user.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.User);
                }

                SetResourceVersion(user);
            });

            return new ScimDataResponse<IEnumerable<ScimUser>>(users);
        }

        public virtual async Task<IScimResponse<ScimUser>> RetrieveUser(IPrincipal principal, string userId)
        {
            var userRecord = await _UserRepository.GetUser(principal, userId).ConfigureAwait(false);
            if (userRecord == null)
                return new ScimErrorResponse<ScimUser>(
                    new ScimError(
                        HttpStatusCode.NotFound,
                        detail: ScimErrorDetail.NotFound(userId)));

            // repository populates meta only if it sets Created and/or LastModified
            if (userRecord.Meta == null)
            {
                userRecord.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.User);
            }

            SetResourceVersion(userRecord);

            return new ScimDataResponse<ScimUser>(userRecord);
        }

        public virtual async Task<IScimResponse<ScimUser>> UpdateUser(IPrincipal principal, ScimUser user)
        {
            return await (await RetrieveUser(principal, user.Id).ConfigureAwait(false))
                .BindAsync<ScimUser, ScimUser>(async userRecord =>
                {
                    user.Groups = userRecord.Groups; // user.Groups is readOnly and used here only for resource versioning
                    user.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.User)
                    {
                        Created = userRecord.Meta.Created,
                    };

                    _CanonicalizationService.Canonicalize(user, ServerConfiguration.GetScimTypeDefinition(user.GetType()));

                    var validator = await _ResourceValidatorFactory.CreateValidator(user).ConfigureAwait(false);
                    var validationResult = (await validator.ValidateUpdateAsync(user, userRecord).ConfigureAwait(false)).ToScimValidationResult();

                    if (!validationResult)
                        return new ScimErrorResponse<ScimUser>(validationResult.Errors.First());

                    // check if we're changing a password
                    if (_PasswordManager.PasswordIsDifferent(user.Password, userRecord.Password))
                    {
                        if (!ServerConfiguration.GetFeature(ScimFeatureType.ChangePassword).Supported)
                        {
                            return new ScimErrorResponse<ScimUser>(
                                new ScimError(
                                    HttpStatusCode.BadRequest,
                                    ScimErrorType.InvalidValue,
                                    "Password change is not supported."));
                        }

                        // if we're not setting password to null, then hash the plainText
                        if (user.Password != null)
                            user.Password = _PasswordManager.CreateHash(user.Password);
                    }

                    SetResourceVersion(user);

                    // if both versions are equal, bypass persistence
                    if (string.Equals(user.Meta.Version, userRecord.Meta.Version))
                        return new ScimDataResponse<ScimUser>(userRecord);

                    var updatedUser = await _UserRepository.UpdateUser(principal, user).ConfigureAwait(false);

                    // set version of updated entity returned by repository
                    SetResourceVersion(updatedUser);

                    return new ScimDataResponse<ScimUser>(updatedUser);
                }).ConfigureAwait(false);
        }
    }
}