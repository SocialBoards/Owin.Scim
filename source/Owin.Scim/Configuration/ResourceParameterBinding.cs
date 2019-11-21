﻿namespace Owin.Scim.Configuration
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http.Controllers;
    using System.Web.Http.Metadata;
    
    using Newtonsoft.Json.Linq;

    using ErrorHandling;

    using Extensions;

    using Model;

    using Newtonsoft.Json;

    using Services;

    public class ResourceParameterBinding : HttpParameterBinding
    {
        private static readonly ConcurrentDictionary<Type, IEnumerable<string>> _RequiredResourceExtensionCache =
            new ConcurrentDictionary<Type, IEnumerable<string>>(); 

        private static readonly HttpMethod _Patch = new HttpMethod("patch");

        private readonly ScimServerConfiguration _ServerConfiguration;

        public ResourceParameterBinding(
            ScimServerConfiguration serverConfiguration,
            HttpParameterDescriptor parameter) 
            : base(parameter)
        {
            _ServerConfiguration = serverConfiguration;
        }

        public override async Task ExecuteBindingAsync(
            ModelMetadataProvider metadataProvider,
            HttpActionContext actionContext,
            CancellationToken cancellationToken)
        {
            var jsonString = await actionContext.Request.Content.ReadAsStringAsync().ConfigureAwait(false);

            var jsonData = JObject.Parse(jsonString);
            var schemasKey = jsonData.FindKeyCaseInsensitive(ScimConstants.Schemas.Key);
            if (schemasKey == null)
            {
                throw new ScimException(HttpStatusCode.BadRequest,
                    ScimErrorDetail.AttributeRequired(ScimConstants.Schemas.Key),
                    ScimErrorType.InvalidValue);
            }

            var schemasValue = jsonData[schemasKey];
            if (schemasValue == null || !schemasValue.HasValues)
            {
                throw new ScimException(HttpStatusCode.BadRequest,
                    ScimErrorDetail.AttributeRequired(ScimConstants.Schemas.Key),
                    ScimErrorType.InvalidValue);
            }

            // determine which concrete resource type to instantiate
            Type schemaType = null;
            foreach (var schemaBindingRule in _ServerConfiguration.SchemaBindingRules)
            {
                if (schemaBindingRule.Predicate(((JArray)schemasValue).ToObject<ISet<string>>(), Descriptor.ParameterType))
                    schemaType = schemaBindingRule.Target;
            }

            if (schemaType == null)
                throw new ScimException(
                    HttpStatusCode.BadRequest,
                    "Unsupported schema.",
                    ScimErrorType.InvalidValue);
            
            if (!Descriptor.ParameterType.IsAssignableFrom(schemaType))
                throw new ScimException(
                    HttpStatusCode.InternalServerError,
                    string.Format(
                        @"The SCIM server's parameter binding rules resulted in a type 
                          which is un-assignable to the controller action's parameter type. 
                          The action's parameter type is '{0}' but the parameter binding rules 
                          resulted in type '{1}'.",
                        Descriptor.ParameterType,
                        schemaType)
                        .RemoveMultipleSpaces());

            // Enforce the request contains all required extensions for the resource.
            var resourceTypeDefinition = (IScimResourceTypeDefinition)_ServerConfiguration.GetScimTypeDefinition(schemaType);
            var requiredExtensions = _RequiredResourceExtensionCache.GetOrAdd(resourceTypeDefinition.DefinitionType, resourceType => resourceTypeDefinition.SchemaExtensions.Where(e => e.Required).Select(e => e.Schema));
            if (requiredExtensions.Any())
            {
                foreach (var requiredExtension in requiredExtensions)
                {
                    // you cannot set a required schema extension to null, e.g. !HasValues
                    if (jsonData[requiredExtension] == null || !jsonData[requiredExtension].HasValues)
                    {
                        // the request will be cut short by ModelBindingResponseAttribute and the response below will be returned
                        SetValue(actionContext, null);
                        actionContext.Response = actionContext.Request.CreateResponse(
                            HttpStatusCode.BadRequest,
                            new ScimError(
                                HttpStatusCode.BadRequest,
                                ScimErrorType.InvalidValue,
                                string.Format(
                                    "'{0}' is a required extension for this resource type '{1}'. The extension must be specified in the request content.", 
                                    requiredExtension, 
                                    _ServerConfiguration.GetSchemaIdentifierForResourceType(schemaType))));
                        return;
                    }
                }
            }
            
            // When no attributes are specified for projection, the response should contain any attributes whose 
            // attribute definition Returned is equal to Returned.Request
            if (actionContext.Request.Method == HttpMethod.Post ||
                actionContext.Request.Method == _Patch ||
                actionContext.Request.Method == HttpMethod.Put)
            {
                var queryOptions = AmbientRequestService.QueryOptions;
                if (!queryOptions.Attributes.Any())
                {
                    // TODO: (DG) if no attributes have been specified, fill the attributes artificially with jsonData keys for attributes defined as Returned.Request
                }
            }
            
            var resource = JsonConvert.DeserializeObject(
                    jsonString,
                    schemaType,
                    Descriptor
                        .Configuration
                        .Formatters
                        .JsonFormatter
                        .SerializerSettings);

            SetValue(actionContext, resource);
        }
    }
}