using System.Security.Principal;

namespace Owin.Scim.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.FSharp.Core;

    using Model.Groups;

    using Querying;

    public interface IGroupService
    {
        Task<IScimResponse<ScimGroup>> CreateGroup(IPrincipal principal, ScimGroup group);

        Task<IScimResponse<ScimGroup>> RetrieveGroup(IPrincipal principal, string groupId);

        Task<IScimResponse<ScimGroup>> UpdateGroup(IPrincipal principal, ScimGroup group);

        Task<IScimResponse<Unit>> DeleteGroup(IPrincipal principal, string groupId);

        Task<IScimResponse<IEnumerable<ScimGroup>>> QueryGroups(IPrincipal principal, ScimQueryOptions options);
    }
}