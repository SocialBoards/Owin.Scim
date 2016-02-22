﻿namespace Owin.Scim.Endpoints
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    using Configuration;

    using Model;

    public class ServiceProviderConfigurationController : ControllerBase
    {
        public ServiceProviderConfigurationController(ScimServerConfiguration scimServerConfiguration)
            : base(scimServerConfiguration)
        {
        }

        [Route("serviceproviderconfig", Name = "ServiceProviderConfig")]
        public async Task<HttpResponseMessage> Get()
        {
            var serviceProviderConfig = (ServiceProviderConfig) ScimServerConfiguration;
            var response = Request.CreateResponse(
                HttpStatusCode.OK, 
                serviceProviderConfig);

            SetLocationHeader(response, serviceProviderConfig, "ServiceProviderConfig");

            return response;
        }
    }
}