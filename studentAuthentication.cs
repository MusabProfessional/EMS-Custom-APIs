using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;

namespace Student_Authentication
{
    public class Student_Authentication : IPlugin
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
            if (context.MessageName.Equals("dst_student_Authentication"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    // Get the input parameters
                    string student_Email = (string)context.InputParameters["Student_Email"];
                    string password = (string)context.InputParameters["password"];
                   

                    tracingService.Trace("Student ID: " + student_Email);
                    tracingService.Trace("Subject Name: " + password);

                    QueryExpression emailQuery = new QueryExpression("contact")
                    {
                        ColumnSet = new ColumnSet("contactid"),
                        Criteria = new FilterExpression(LogicalOperator.And)
                        {
                            Conditions =
                {
                    new ConditionExpression("emailaddress1", ConditionOperator.Equal, student_Email)
                }
                        }
                    };

                    EntityCollection emailResults = service.RetrieveMultiple(emailQuery);

                    if (emailResults.Entities.Count == 0)
                    {
                        // No such email
                        context.OutputParameters["ErrorMessage"] = "Email not found";
                        return;
                    }


                    // FetchXML query to retrieve attendance records
                    string fetchXml = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='contact'>
                            <attribute name='fullname' />
                            <attribute name='telephone1' />
                            <attribute name='contactid' />
                            <attribute name='cdst_studentid' />
                            <attribute name='cdst_sms_schoolclass' />
                            <attribute name='cdst_internalscore' />
                            <attribute name='cdst_attendancepercentage' />
                            <attribute name='entityimage' />
                            <order attribute='fullname' descending='false' />
                            <filter type='and'>
                              <condition attribute='emailaddress1' operator='eq' value='{student_Email}' />
                              <condition attribute='cdst_password' operator='eq' value='{password}' />
                            </filter>
                            <link-entity name='sms_schoolclass' from='sms_schoolclassid' to='cdst_sms_schoolclass' link-type='inner' alias='ai'>
                                <attribute name='sms_name' />
                                  <filter type='and'>
                                    <condition attribute='sms_name' operator='not-null' />
                                  </filter>
                                </link-entity>
                            <link-entity name='account' from='accountid' to='parentcustomerid' link-type='inner' alias='aj'>
                                  <attribute name='accountnumber' />
                                </link-entity>
                                   </entity>
                        </fetch>";

                    tracingService.Trace("FetchXML: " + fetchXml);
                    EntityCollection authenticate = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of attendance records retrieved: " + authenticate.Entities.Count);

                    if (authenticate.Entities.Count == 0)
                    {
                        // No matching record found - invalid credentials
                        context.OutputParameters["ErrorMessage"] = "Incorrect Password. Please try again.";
                    }
                    else
                    {
                        // Success
                        context.OutputParameters["Student_Data"] = authenticate;
                    }

                }
                catch (Exception ex)
                {
                    tracingService.Trace("student Authenticate: {0}", ex.ToString());
                    throw new InvalidPluginExecutionException("An error occurred in Student AUthenticate.", ex);
                }
            }
            else
            {
                tracingService.Trace("student Authenticate plugin is not associated with the expected message or is not registered for the main operation.");
            }
        }
    }
}