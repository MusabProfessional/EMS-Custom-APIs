using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using Newtonsoft.Json;

namespace Enrollments_History
{
    public class Enrollments_History : IPlugin
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
            if (context.MessageName.Equals("dst_enrollmentHistoryDynamicAPI"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    // Get the input parameters
                    string studentID = (string)context.InputParameters["studentID_Enrollment"];

                    tracingService.Trace("Student ID: " + studentID);

                    // FetchXML query to retrieve enrollment records
                    string fetchXml = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='cdst_enrollmenthistorytable'>
                                <attribute name='cdst_enrollmenthistorytableid' />
                                <attribute name='cdst_fieldname' />
                                <attribute name='cdst_fieldlogicalname' />
                                <attribute name='cdst_fieldtype' />
                                <attribute name='cdst_tabledisplayname' />
                                <attribute name='cdst_tablelogicalname' />
                                <attribute name='cdst_sequence' />
                                <attribute name='cdst_linkfromattributename' />
                                <attribute name='cdst_linktoattributename' />
                                <attribute name='cdst_linkfromentityname' />
                                <attribute name='cdst_linktoentityname' />
                                <attribute name='cdst_entityalias' />
                              </entity>
                            </fetch>";

                    tracingService.Trace("FetchXML: " + fetchXml);
                    EntityCollection enrollment = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of enrollment records retrieved: " + enrollment.Entities.Count);

                    // Collections to store field logical names and lookup fields
                    List<string> fieldLogicalNames = new List<string>();
                    List<string> lookupFieldLogicalNames = new List<string>();
                    List<LinkEntity> linkEntities = new List<LinkEntity>();

                    foreach (Entity records in enrollment.Entities)
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
                    QueryExpression query = new QueryExpression("cdst_enrollment")
                    {
                        ColumnSet = columns,
                    };

                    // Add LinkEntity for student
                    LinkEntity studentLink = new LinkEntity
                    {
                        LinkFromEntityName = "cdst_enrollment",
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
                    
                    //--------------------------------------------------------------
                    foreach(Entity i in enrollment.Entities) {
                        OptionSetValue fieldTypeOptionSet = i.GetAttributeValue<OptionSetValue>("cdst_fieldtype");
                        if (fieldTypeOptionSet.Value == 940020021)
                        {

                            string linkFromAttributeName = i.GetAttributeValue<string>("cdst_linkfromattributename");
                            string linkToAttributeName = i.GetAttributeValue<string>("cdst_linktoattributename");
                            string linkFromEntityName = i.GetAttributeValue<string>("cdst_linkfromentityname");
                            string linkToEntityName = i.GetAttributeValue<string>("cdst_linktoentityname");
                            string entityAlias = i.GetAttributeValue<string>("cdst_entityalias");

                            LinkEntity dynamicLinkEntity = new LinkEntity
                            {
                                LinkFromEntityName = linkFromEntityName,
                                LinkFromAttributeName = linkFromAttributeName,
                                LinkToEntityName = linkToEntityName,
                                LinkToAttributeName = linkToAttributeName,
                                JoinOperator = JoinOperator.LeftOuter,
                                Columns = new ColumnSet("sms_name"),
                                EntityAlias = entityAlias
                            };

                            tracingService.Trace($"Created LinkEntity: {linkFromEntityName} to {linkToEntityName}");
                            linkEntities.Add(dynamicLinkEntity);
                        }
                    }
                    foreach (var linkEntity in linkEntities)
                    {
                        query.LinkEntities.Add(linkEntity);
                    }

                    // Execute the query to retrieve the filtered records
                    EntityCollection results = service.RetrieveMultiple(query);       

                    tracingService.Trace("Results with lookup values included in OutputParameters.");

                    // Set the results with lookup names included
                    context.OutputParameters["Enrollments_DynamicAPI"] = results;
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Enrollments_History: {0}", ex.ToString());
                    throw new InvalidPluginExecutionException("An error occurred in Enrollment Dynamic API.", ex);
                }
            }
            else
            {
                tracingService.Trace("Enrollments_History plugin is not associated with the expected message or is not registered for the main operation.");
            }
        }
    }
}
