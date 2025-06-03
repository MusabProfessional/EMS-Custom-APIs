using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;

namespace Figma
{
    public class FeatureDetails : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service


            ITracingService tracingService =(ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.  
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            IOrganizationServiceFactory serviceFactory =
                    (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.MessageName.Equals("dst_FeatureDetails"))
            {

                try
                {

                    tracingService.Trace("Plugin execution started.");
                    //  string featureName = (string)context.InputParameters["featureName"];
                    //  tracingService.Trace("FeatureName: " + featureName);

                    string fetchXml = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
  <entity name='cdst_figmaconfiguration'>
    <attribute name='cdst_figmaconfigurationid' />
    <attribute name='cdst_name' />
    <attribute name='createdon' />
    <attribute name='cdst_relatedtable' />
    <attribute name='cdst_colorhexadecimalcode' />
     <attribute name='cdst_featuresattachmentimage' />
    <order attribute='cdst_name' descending='false' /> 
  </entity>
</fetch>";

                    tracingService.Trace("FetchXML: " + fetchXml);
                    EntityCollection features = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of contacts retrieved: " + features.Entities.Count);

                    context.OutputParameters["FeatureDetails"] = features;
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Sample_CustomAPIExample: {0}", ex.ToString());
                    throw new InvalidPluginExecutionException("An error occurred in Sample_CustomAPIExample.", ex);
                }
            }
            else
            {
                tracingService.Trace("Sample_CustomAPIExample plug-in is not associated with the expected message or is not registered for the main operation.");
            }
        }
    }
}
