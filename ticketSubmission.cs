using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using Newtonsoft.Json;

public class CreateCaseFromAppPlugin : IPlugin
{
    public void Execute(IServiceProvider serviceProvider)
    {
        // Tracing service
        ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

        // Execution context
        IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

        // Organization service
        IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
        IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

        if (context.MessageName.Equals("dst_CreateCaseFromApp"))
        {
            try
            {
                // Get input parameters
                string title = context.InputParameters["title_case"] as string;
                string description = context.InputParameters["description_case"] as string;
                string customerIdStr = context.InputParameters["customerId_case"] as string;

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(customerIdStr))
                    throw new InvalidPluginExecutionException("Title and Customer ID are required.");

                tracingService.Trace("Creating case for customer ID: " + customerIdStr);

                // Parse customer ID
                Guid customerId = Guid.Parse(customerIdStr);

                // Create the case (incident)
                Entity caseEntity = new Entity("incident");
                caseEntity["title"] = title;
                caseEntity["description"] = description;
                caseEntity["customerid"] = new EntityReference("contact", customerId); // or "account" if you use account

                Guid caseId = service.Create(caseEntity);

                context.OutputParameters["caseId_case"] = "Your Ticket has been Submitted";//caseId.ToString();

                tracingService.Trace("Case created successfully with ID: " + caseId);
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error creating case: " + ex.ToString());
                throw new InvalidPluginExecutionException("An error occurred while creating the case.", ex);
            }
        }
    }
}
