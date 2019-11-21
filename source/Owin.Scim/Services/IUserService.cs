using System.Security.Principal;

namespace Owin.Scim.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.FSharp.Core;
    
    using Model.Users;

    using Querying;

    public interface IUserService
    {
        Task<IScimResponse<ScimUser>> CreateUser(IPrincipal principal, ScimUser user);

        Task<IScimResponse<ScimUser>> RetrieveUser(IPrincipal principal, string userId);

        Task<IScimResponse<ScimUser>> UpdateUser(IPrincipal principal, ScimUser user);

        Task<IScimResponse<Unit>> DeleteUser(IPrincipal principal, string userId);

        Task<IScimResponse<IEnumerable<ScimUser>>> QueryUsers(IPrincipal principal, ScimQueryOptions options);
    }
}