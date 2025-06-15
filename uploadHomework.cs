using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using Newtonsoft.Json;

namespace UploadHomework
{
    public class homework : IPlugin
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
            if (context.MessageName.Equals("dst_UploadHomework"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    // Get the input parameters
                    string studentID = (string)context.InputParameters["studentID_Homework"];
                    string TargetLogicalName = (string)context.InputParameters["TargetLogicalName_Homework"];
                    string fileName = (string)context.InputParameters["FileName_Homework"];
                    string fileContentBase64 = (string)context.InputParameters["fileContentBase64_Homework"];
                    string mimetype = (string)context.InputParameters["mimetype_Homework"];
                    string subject = (string)context.InputParameters["subject_Homework"];
                    string targetLogicalName2 = (string)context.InputParameters["TargetLogicalName_Homework2"];
                    string homeworkID = (string)context.InputParameters["HomeworkID"];


                    tracingService.Trace("Student ID: " + studentID);

                    // FetchXML query to retrieve enrollment records
                    string fetchXml = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                  <entity name='contact'>
                    <attribute name='fullname' />
                    <attribute name='telephone1' />
                    <attribute name='contactid' />
                    <attribute name='emailaddress1' />
                    <order attribute='fullname' descending='false' />
                    <filter type='and'>
                      <condition attribute='cdst_studentid' operator='eq' value='{studentID}'/>
                    </filter>
                  </entity>
                </fetch>";

                    tracingService.Trace("FetchXML: " + fetchXml);
                    EntityCollection studentDetails = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of enrollment records retrieved: " + studentDetails.Entities.Count);



                    if (string.IsNullOrEmpty(studentID))
                        throw new InvalidPluginExecutionException("StudentID parameter is required.");

                    // Retrieve contact by email
                    var query = new QueryExpression("contact")
                    {
                        ColumnSet = new ColumnSet("contactid"),
                        Criteria =
                        {
                            Conditions =
                            {
                                new ConditionExpression("cdst_studentid", ConditionOperator.Equal, studentID)
                            }
                        }
                    };

                    var contacts = service.RetrieveMultiple(query);

                    if (contacts.Entities.Count == 0)
                        throw new InvalidPluginExecutionException($"No contact found with studentID: {studentID}");

                    var contact = contacts.Entities[0];
                    var contactId = contact.Id;


                    //-------------------------------------------------------------------------------------
                    var homeworkQuery = new QueryExpression(targetLogicalName2)
                    {
                        ColumnSet = new ColumnSet("cdst_homeworkid"), 
                        Criteria =
                            {
                                Conditions =
                                {
                                    new ConditionExpression("cdst_id", ConditionOperator.Equal, homeworkID)
                                }
                            }
                                            };

                    var homeworkRecords = service.RetrieveMultiple(homeworkQuery);

                    if (homeworkRecords.Entities.Count == 0)
                        throw new InvalidPluginExecutionException($"No homework record found with ID: {homeworkID}");

                    var homeworkRecord = homeworkRecords.Entities[0];
                    var homeworkRecordId = homeworkRecord.Id;




                    byte[] fileBytes = Convert.FromBase64String(fileContentBase64);

                var note = new Entity("annotation");
                note["objectid"] = new EntityReference(TargetLogicalName, contactId);
                note["subject"] = subject;
                note["filename"] = fileName;
                note["mimetype"] = mimetype;
                note["documentbody"] = fileContentBase64;

                 service.Create(note);


                    //---------------For Homework
                    var noteForHomework = new Entity("annotation");
                    noteForHomework["objectid"] = new EntityReference(targetLogicalName2, homeworkRecordId);
                    noteForHomework["subject"] = subject;
                    noteForHomework["filename"] = fileName;
                    noteForHomework["mimetype"] = mimetype;
                    noteForHomework["documentbody"] = fileContentBase64;
                    service.Create(noteForHomework);



                    tracingService.Trace("Results with lookup values included in OutputParameters.");

                    // Set the results with lookup names included
                    context.OutputParameters["uploadHommework_Output"] = "Created Successfully";

        
                }
                catch (Exception ex)
                {
                    tracingService.Trace("uploadHomework: {0}", ex.ToString());
                    throw new InvalidPluginExecutionException("An error occurred in Upload Homework API.", ex);
                }
            }
            else
            {
                tracingService.Trace("Upload Homework plugin is not associated with the expected message or is not registered for the main operation.");
            }
        }
    }
}