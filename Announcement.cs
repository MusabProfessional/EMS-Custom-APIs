using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using Newtonsoft.Json;

namespace Announcement
{
    public class Class1 : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService =
                (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider
            IPluginExecutionContext context =
                (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Obtain the organization service reference
            IOrganizationServiceFactory serviceFactory =
                (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            // Check the message name
            if (context.MessageName.Equals("dst_AnnouncementAPi"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    // Get the input parameters
                    string studentID = (string)context.InputParameters["studentID_Announcement"];

                    tracingService.Trace("Student ID: " + studentID);

                    // FetchXML query to retrieve enrollment records
                    string fetchXml = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='cdst_annoucementapiconfig'>
                                <attribute name='cdst_annoucementapiconfigid' />
                                <attribute name='cdst_fieldlogicalname' />
                                <attribute name='createdon' />
                                <attribute name='cdst_tablename' />
                                <attribute name='cdst_tablelogicalname' />
                                <attribute name='cdst_fieldtype' />
                                <attribute name='cdst_fieldname' />
                                <attribute name='cdst_linktoentityname' />
                                <attribute name='cdst_linktoattributename' />
                                <attribute name='cdst_linkfromentityname' />
                                <attribute name='cdst_linkfromattributename' />
                                <attribute name='cdst_entityalias' />
                              </entity>
                            </fetch>";

                    tracingService.Trace("FetchXML: " + fetchXml);
                    EntityCollection announcements = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of enrollment records retrieved: " + announcements.Entities.Count);

                    // Collections to store field logical names and lookup fields
                    List<string> fieldLogicalNames = new List<string>();
                    List<string> lookupFieldLogicalNames = new List<string>();
                    List<LinkEntity> linkEntities = new List<LinkEntity>();

                    foreach (Entity records in announcements.Entities)
                    {
                        if (records.Attributes.Contains("cdst_fieldlogicalname"))
                        {
                            string fieldLogicalName = records.GetAttributeValue<string>("cdst_fieldlogicalname");
                            OptionSetValue fieldTypeOptionSet = records.GetAttributeValue<OptionSetValue>("cdst_fieldtype");
                            fieldLogicalNames.Add(fieldLogicalName);
                        }
                    }

                    tracingService.Trace("Extracted field logical names: " + string.Join(", ", fieldLogicalNames));
                    tracingService.Trace("Extracted lookup field logical names: " + string.Join(", ", lookupFieldLogicalNames));

                    ColumnSet columns = new ColumnSet(fieldLogicalNames.ToArray());

                    // Create QueryExpression with link entities
                    QueryExpression query = new QueryExpression("cdst_annoucement")
                    {
                        ColumnSet = columns,
                    };

                    // Add LinkEntity for student
                    LinkEntity studentLink = new LinkEntity
                    {
                        LinkFromEntityName = "cdst_annoucement",
                        LinkFromAttributeName = "cdst_student",
                        LinkToEntityName = "contact",
                        LinkToAttributeName = "contactid",
                        JoinOperator = JoinOperator.Inner,
                        LinkCriteria =
                        {
                            Conditions =
                            {
                                new ConditionExpression("cdst_studentid", ConditionOperator.Equal, studentID)
                            }
                        }
                    };
                    query.LinkEntities.Add(studentLink);

                    // Execute the query to retrieve the filtered records
                    EntityCollection results = service.RetrieveMultiple(query);

                    // Set the isSuccess output parameter based on whether data was found
                    bool isSuccess = results.Entities.Count > 0;
                    context.OutputParameters["isSuccess_Annoucement"] = isSuccess;

                    tracingService.Trace($"isSuccess set to: {isSuccess}");
                    tracingService.Trace("Results with lookup values included in OutputParameters.");

                    // Set the results with lookup names included
                    context.OutputParameters["Announcement_DynamicAPI"] = results;
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Anouncements: {0}", ex.ToString());
                    // Set isSuccess to false in case of error
                    context.OutputParameters["isSuccess"] = false;
                    throw new InvalidPluginExecutionException("An error occurred in Announcements Dynamic API.", ex);
                }
            }
            else
            {
                tracingService.Trace("Announcement plugin is not associated with the expected message or is not registered for the main operation.");
            }
        }
    }
}