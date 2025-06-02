using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using System.IO;
using Microsoft.Crm.Sdk.Messages; // Add this for file block messages

namespace HomeworkAPi
{
    public class Homework : IPlugin
    {
        // Add the helper method for file download and base64 conversion
        private string DownloadFileAndGetBase64(IOrganizationService service, string entityName, Guid recordGuid, string fileAttributeName)
        {
            var initializeFileBlocksDownloadRequest = new InitializeFileBlocksDownloadRequest
            {
                Target = new EntityReference(entityName, recordGuid),
                FileAttributeName = fileAttributeName
            };

            var initializeFileBlocksDownloadResponse = (InitializeFileBlocksDownloadResponse)
                service.Execute(initializeFileBlocksDownloadRequest);

            var downloadBlockRequest = new DownloadBlockRequest
            {
                FileContinuationToken = initializeFileBlocksDownloadResponse.FileContinuationToken
            };

            var downloadBlockResponse = (DownloadBlockResponse)service.Execute(downloadBlockRequest);

            byte[] fileBytes = downloadBlockResponse.Data;

            // Convert to Base64
            string base64FileContent = Convert.ToBase64String(fileBytes);

            return base64FileContent;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.MessageName.Equals("dst_homeworkDynamicAPI"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    string classId = (string)context.InputParameters["classID_Homework"];
                    tracingService.Trace("Class ID: " + classId);

                    // Retrieve homework configuration
                    string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='cdst_homeworkconfigtable'>
                            <attribute name='cdst_fieldlogicalname' />
                            <attribute name='cdst_fieldtype' />
                            <attribute name='cdst_linkfromattributename' />
                            <attribute name='cdst_linktoattributename' />
                            <attribute name='cdst_linkfromentityname' />
                            <attribute name='cdst_linktoentityname' />
                            <attribute name='cdst_entityalias' />
                          </entity>
                        </fetch>";

                    EntityCollection homeworks = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of config records retrieved: " + homeworks.Entities.Count);

                    // Prepare fields and links
                    List<string> fieldLogicalNames = new List<string>();
                    List<LinkEntity> linkEntities = new List<LinkEntity>();
                    List<string> fileFieldNames = new List<string>(); // For file columns

                    foreach (Entity record in homeworks.Entities)
                    {
                        if (record.Attributes.Contains("cdst_fieldlogicalname"))
                        {
                            string fieldLogicalName = record.GetAttributeValue<string>("cdst_fieldlogicalname");
                            fieldLogicalNames.Add(fieldLogicalName);

                            OptionSetValue fieldType = record.GetAttributeValue<OptionSetValue>("cdst_fieldtype");
                            if (fieldType?.Value == 940020021) // Lookup type
                            {
                                LinkEntity link = new LinkEntity
                                {
                                    LinkFromEntityName = record.GetAttributeValue<string>("cdst_linkfromentityname"),
                                    LinkFromAttributeName = record.GetAttributeValue<string>("cdst_linkfromattributename"),
                                    LinkToEntityName = record.GetAttributeValue<string>("cdst_linktoentityname"),
                                    LinkToAttributeName = record.GetAttributeValue<string>("cdst_linktoattributename"),
                                    JoinOperator = JoinOperator.LeftOuter,
                                    Columns = new ColumnSet("sms_name"),
                                    EntityAlias = record.GetAttributeValue<string>("cdst_entityalias")
                                };
                                linkEntities.Add(link);
                            }
                            // Detect file columns (adjust OptionSet value if needed)
                            if (fieldType?.Value == 940020019)
                            {
                                fileFieldNames.Add(fieldLogicalName);
                            }
                            if (fieldType?.Value == 940020007 && fieldLogicalName == "cdst_assignmentbase64")
                            {
                                string assignmentBase64FieldName = fieldLogicalName;
                                
                            }
                        }
                    }

                    // Main query for homework
                    QueryExpression query = new QueryExpression("cdst_homework")
                    {
                        ColumnSet = new ColumnSet(fieldLogicalNames.ToArray())
                    };

                    // Add class filter
                    query.LinkEntities.Add(new LinkEntity
                    {
                        LinkFromEntityName = "cdst_homework",
                        LinkFromAttributeName = "cdst_class",
                        LinkToEntityName = "sms_schoolclass",
                        LinkToAttributeName = "sms_schoolclassid",
                        JoinOperator = JoinOperator.Inner,
                        LinkCriteria = { Conditions = { new ConditionExpression("sms_name", ConditionOperator.Equal, classId) } }
                    });

                    // Add dynamic links
                    foreach (var link in linkEntities)
                    {
                        query.LinkEntities.Add(link);
                    }

                    // Get subjects
                    string fetchXml2 = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                  <entity name='sms_classsubject'>
                    <attribute name='sms_classsubjectid' />
                    <attribute name='sms_name' /> 
                    <order attribute='sms_name' descending='false' />
                    <link-entity name='sms_schoolclass' from='sms_schoolclassid' to='sms_schoolclassid' link-type='inner' alias='ab'>
                      <filter type='and'>
                        <condition attribute='sms_name' operator='eq' value='{classId}' />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>";

                    EntityCollection class_subjects = service.RetrieveMultiple(new FetchExpression(fetchXml2));

                    // Get homework records
                    EntityCollection results = service.RetrieveMultiple(query);

                    // Add base64 file content for each file column
                    foreach (var entity in results.Entities)
                    {
                        foreach (var fileField in fileFieldNames)
                        {
                            if (entity.Attributes.Contains(fileField))
                            {
                                try
                                {
                                    string base64Content = DownloadFileAndGetBase64(
                                        service,
                                        entity.LogicalName,
                                        entity.Id,
                                        fileField
                                    );
                                    if (!string.IsNullOrEmpty(base64Content))
                                    {
                                        entity.Attributes["cdst_assignmentbase64"] = base64Content;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tracingService.Trace($"Error downloading file for field {fileField} on entity {entity.Id}: {ex}");
                                }
                            }
                        }
                    }

                    // Set output parameters
                    context.OutputParameters["Homework_ResponseAPI"] = results;
                    context.OutputParameters["Homework_Subjects"] = class_subjects;
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Error: {0}", ex.ToString());
                    throw new InvalidPluginExecutionException("An error occurred in Homework Dynamic API.", ex);
                }
            }
        }
    }
}