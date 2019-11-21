﻿using System.Security.Principal;

namespace Owin.Scim.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    using Microsoft.FSharp.Core;
    
    using Configuration;
    using Extensions;
    using ErrorHandling;
    using Model;
    using Model.Groups;

    using NContext.Extensions;

    using Querying;

    using Repository;
    using Validation;

    public class GroupService : ServiceBase, IGroupService
    {
        private readonly ICanonicalizationService _CanonicalizationService;

        private readonly IResourceValidatorFactory _ResourceValidatorFactory;

        private readonly IGroupRepository _GroupRepository;

        public GroupService(
            ScimServerConfiguration serverConfiguration,
            IResourceVersionProvider versionProvider,
            IResourceValidatorFactory resourceValidatorFactory,
            ICanonicalizationService canonicalizationService,
            IGroupRepository groupRepository) 
            : base(serverConfiguration, versionProvider)
        {
            _GroupRepository = groupRepository;
            _ResourceValidatorFactory = resourceValidatorFactory;
            _CanonicalizationService = canonicalizationService;
        }

        public virtual async Task<IScimResponse<ScimGroup>> CreateGroup(IPrincipal principal, ScimGroup group)
        {
            _CanonicalizationService.Canonicalize(group, ServerConfiguration.GetScimTypeDefinition(group.GetType()));

            var validator = await _ResourceValidatorFactory.CreateValidator(@group).ConfigureAwait(false);
            var validationResult = (await validator.ValidateCreateAsync(@group).ConfigureAwait(false)).ToScimValidationResult();

            if (!validationResult)
                return new ScimErrorResponse<ScimGroup>(validationResult.Errors.First());

            group.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.Group);
            
            var groupRecord = await _GroupRepository.CreateGroup(principal, @group).ConfigureAwait(false);

            SetResourceVersion(groupRecord);

            return new ScimDataResponse<ScimGroup>(groupRecord);
        }

        public virtual async Task<IScimResponse<ScimGroup>> RetrieveGroup(IPrincipal principal, string groupId)
        {
            var userRecord = SetResourceVersion(await _GroupRepository.GetGroup(principal, groupId).ConfigureAwait(false));
            if (userRecord == null)
                return new ScimErrorResponse<ScimGroup>(
                    new ScimError(
                        HttpStatusCode.NotFound,
                        detail: ScimErrorDetail.NotFound(groupId)));

            // repository populates meta only if it sets Created and/or LastModified
            if (userRecord.Meta == null)
            {
                userRecord.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.Group);
            }

            return new ScimDataResponse<ScimGroup>(userRecord);
        }

        public virtual async Task<IScimResponse<ScimGroup>> UpdateGroup(IPrincipal principal, ScimGroup group)
        {
            return await (await RetrieveGroup(principal, @group.Id).ConfigureAwait(false))
                .BindAsync<ScimGroup, ScimGroup>(async groupRecord =>
                {
                    @group.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.Group)
                    {
                        Created = groupRecord.Meta.Created,
                        LastModified = groupRecord.Meta.LastModified
                    };

                    _CanonicalizationService.Canonicalize(@group, ServerConfiguration.GetScimTypeDefinition(@group.GetType()));

                    var validator = await _ResourceValidatorFactory.CreateValidator(@group).ConfigureAwait(false);
                    var validationResult = (await validator.ValidateUpdateAsync(@group, groupRecord).ConfigureAwait(false)).ToScimValidationResult();

                    if (!validationResult)
                        return new ScimErrorResponse<ScimGroup>(validationResult.Errors.First());

                    SetResourceVersion(@group);

                    // if both versions are equal, bypass persistence
                    if (string.Equals(@group.Meta.Version, groupRecord.Meta.Version))
                        return new ScimDataResponse<ScimGroup>(groupRecord);

                    var updatedGroup = await _GroupRepository.UpdateGroup(principal, @group).ConfigureAwait(false);

                    // set version of updated entity returned by repository
                    SetResourceVersion(updatedGroup);

                    return new ScimDataResponse<ScimGroup>(updatedGroup);
                }).ConfigureAwait(false);
        }

        public virtual async Task<IScimResponse<Unit>> DeleteGroup(IPrincipal principal, string groupId)
        {
            var groupExists = await _GroupRepository.GroupExists(principal, groupId).ConfigureAwait(false);
            if (!groupExists)
                return new ScimErrorResponse<Unit>(
                    new ScimError(
                        HttpStatusCode.NotFound,
                        detail: ScimErrorDetail.NotFound(groupId)));

            await _GroupRepository.DeleteGroup(principal, groupId).ConfigureAwait(false);

            return new ScimDataResponse<Unit>(default(Unit));
        }

        public virtual async Task<IScimResponse<IEnumerable<ScimGroup>>> QueryGroups(IPrincipal principal, ScimQueryOptions options)
        {
            var groups = await _GroupRepository.QueryGroups(principal, options).ConfigureAwait(false) ?? new List<ScimGroup>();
            groups.ForEach(group =>
            {
                // repository populates meta only if it sets Created and/or LastModified
                if (group.Meta == null)
                {
                    group.Meta = new ResourceMetadata(ScimConstants.ResourceTypes.Group);
                }

                SetResourceVersion(group);
            });

            return new ScimDataResponse<IEnumerable<ScimGroup>>(groups);
        }
    }
}