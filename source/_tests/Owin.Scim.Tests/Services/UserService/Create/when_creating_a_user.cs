using System.Security.Permissions;
using System.Security.Principal;

namespace Owin.Scim.Tests.Services.UserService.Create
{
    using System;
    using System.Threading.Tasks;
    
    using Configuration;

    using Extensions;

    using FakeItEasy;

    using Machine.Specifications;

    using Model.Users;

    using Repository;

    using Scim.Security;
    using Scim.Services;

    using Validation.Users;

    public class when_creating_a_user
    {
        Establish context = () =>
        {
            ServerConfiguration = new ScimServerConfiguration().WithTypeDefinitions();
            UserRepository = A.Fake<IUserRepository>();
            GroupRepository = A.Fake<IGroupRepository>();
            PasswordManager = A.Fake<IManagePasswords>();
            Principal  = A.Fake<IPrincipal>();

            A.CallTo(() => UserRepository.IsUserNameAvailable(A<IPrincipal>._, A<string>._))
                .Returns(true);
            
            A.CallTo(() => UserRepository.CreateUser(A<IPrincipal>._, A<ScimUser>._))
                .ReturnsLazily(c =>
                {
                    var user = (ScimUser) c.Arguments[0];
                    user.Id = Guid.NewGuid().ToString("N");

                    return Task.FromResult(user);
                });

            var etagProvider = A.Fake<IResourceVersionProvider>();
            var canonicalizationService = A.Fake<DefaultCanonicalizationService>(o => o.CallsBaseMethods());

            _UserService = new UserService(
                ServerConfiguration,
                etagProvider,
                canonicalizationService,
                new UserValidatorFactory(ServerConfiguration, UserRepository, PasswordManager), 
                UserRepository,
                PasswordManager);
        };

        public static IPrincipal Principal { get; set; }

        Because of = async () => Result = await _UserService.CreateUser(A<IPrincipal>._, ClientUserDto).AwaitResponse().AsTask;

        protected static ScimUser ClientUserDto;

        protected static ScimServerConfiguration ServerConfiguration;

        protected static IUserRepository UserRepository;

        protected static IGroupRepository GroupRepository;

        protected static IManagePasswords PasswordManager;

        protected static IScimResponse<ScimUser> Result;

        private static IUserService _UserService;
    }
}