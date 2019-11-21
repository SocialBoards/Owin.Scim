using System.Security.Principal;

namespace Owin.Scim.Tests.Services.UserService.Update
{
    using System.Threading.Tasks;

    using Canonicalization;

    using Configuration;

    using FakeItEasy;

    using Machine.Specifications;

    using Model.Users;

    using Repository;

    using Scim.Security;
    using Scim.Services;

    using Validation.Users;

    public class when_updating_a_user
    {
        private Establish context = () =>
        {
            ServerConfiguration = new ScimServerConfiguration();
            UserRepository = A.Fake<IUserRepository>();
            GroupRepository = A.Fake<IGroupRepository>();
            PasswordManager = A.Fake<IManagePasswords>();
            Principal  = A.Fake<IPrincipal>();

            A.CallTo(() => UserRepository.UpdateUser(A<IPrincipal>._, A<ScimUser>._))
                .ReturnsLazily(c => Task.FromResult((ScimUser) c.Arguments[0]));

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

        Because of = async () => Result = await _UserService.UpdateUser(Principal, ClientUserDto).AwaitResponse().AsTask;

        protected static ScimUser ClientUserDto;

        protected static ScimServerConfiguration ServerConfiguration;

        protected static IUserRepository UserRepository;

        protected static IGroupRepository GroupRepository;

        protected static IManagePasswords PasswordManager;

        protected static IScimResponse<ScimUser> Result;

        private static IUserService _UserService;
    }
}