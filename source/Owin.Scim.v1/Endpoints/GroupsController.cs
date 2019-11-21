﻿namespace Owin.Scim.v1.Endpoints
{
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    using Configuration;

    using Extensions;

    using Model;

    using NContext.Extensions;

    using Querying;

    using Scim.Endpoints;
    using Scim.Services;

    [RoutePrefix(ScimConstantsV1.Endpoints.Groups)]
    public class GroupsController : ScimControllerBase
    {
        public const string RetrieveGroupRouteName = @"RetrieveGroup1";

        private readonly IGroupService _GroupService;

        public GroupsController(
            ScimServerConfiguration serverConfiguration,
            IGroupService groupService) 
            : base(serverConfiguration)
        {
            _GroupService = groupService;
        }

        [Route(Name = "CreateGroup")]
        public async Task<HttpResponseMessage> Post(ScimGroup1 groupDto)
        {
            return (await _GroupService.CreateGroup(User, groupDto))
                .Let(group => SetMetaLocation(group, RetrieveGroupRouteName, new { groupId = group.Id }))
                .ToHttpResponseMessage(Request, (group, response) =>
                {
                    response.StatusCode = HttpStatusCode.Created;

                    SetContentLocationHeader(response, RetrieveGroupRouteName, new { groupId = group.Id });
                    SetETagHeader(response, group);
                });
        }

        [Route("{groupId}", Name = RetrieveGroupRouteName)]
        public async Task<HttpResponseMessage> Get(string groupId)
        {
            return (await _GroupService.RetrieveGroup(User, groupId))
                .Let(group => SetMetaLocation(group, RetrieveGroupRouteName, new { groupId = group.Id }))
                .Let(group =>
                    group.Members?.ForEach(m =>
                        m.Ref = m.Type == ScimConstants.ResourceTypes.User
                            ? GetUserUri(UsersController.RetrieveUserRouteName, m.Value)
                            : GetGroupUri(RetrieveGroupRouteName, m.Value)))
                .ToHttpResponseMessage(Request, (group, response) =>
                {
                    SetContentLocationHeader(response, RetrieveGroupRouteName, new { groupId = group.Id });
                    SetETagHeader(response, group);
                });
        }

        [AcceptVerbs("GET")]
        [Route]
        public Task<HttpResponseMessage> GetQuery(ScimQueryOptions queryOptions)
        {
            return Query(queryOptions);
        }

        [NonAction]
        private async Task<HttpResponseMessage> Query(ScimQueryOptions options)
        {
            return (await _GroupService.QueryGroups(User, options))
                .Let(groups => groups.ForEach(group => SetMetaLocation(group, RetrieveGroupRouteName, new { groupId = group.Id })))
                .Let(groups => groups.ForEach(group =>
                {
                    // needed to materialize ienumerable, otherwise it did not work
                    var members = group.Members?.ToList();
                    members?.ForEach(m =>
                        m.Ref = m.Type == ScimConstants.ResourceTypes.User
                            ? GetUserUri(UsersController.RetrieveUserRouteName, m.Value)
                            : GetGroupUri(RetrieveGroupRouteName, m.Value));
                    group.Members = members;
                }))
                .Bind(
                    groups =>
                    new ScimDataResponse<ScimListResponse>(
                        new ScimListResponse1(groups)
                        {
                            StartIndex = options.StartIndex,
                            ItemsPerPage = options.Count
                        }))
                .ToHttpResponseMessage(Request);
        }

        [AcceptVerbs("PUT", "OPTIONS")]
        [Route("{groupId}")]
        public async Task<HttpResponseMessage> Put(string groupId, ScimGroup1 groupDto)
        {
            groupDto.Id = groupId;

            return (await _GroupService.UpdateGroup(User, groupDto))
                .Let(group => SetMetaLocation(group, RetrieveGroupRouteName, new { groupId = group.Id }))
                .ToHttpResponseMessage(Request, (group, response) =>
                {
                    SetContentLocationHeader(response, RetrieveGroupRouteName, new { groupId = group.Id });
                    SetETagHeader(response, group);
                });
        }

        [Route("{groupId}")]
        public async Task<HttpResponseMessage> Delete(string groupId)
        {
            return (await _GroupService.DeleteGroup(User, groupId))
                .ToHttpResponseMessage(Request, HttpStatusCode.NoContent);
        }
    }
}