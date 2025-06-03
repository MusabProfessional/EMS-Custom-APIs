using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk;
using System;
using Newtonsoft.Json;

namespace subjectAttendance
{
    public class subjectAttendance_student : IPlugin
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
            if (context.MessageName.Equals("dst_studentAttendance_subject"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    // Get the input parameters
                    string studentID = (string)context.InputParameters["Std_id_attendance"];
                    DateTime FromDate = (DateTime)context.InputParameters["FromDate_attendance"];

                    tracingService.Trace("Student ID: " + studentID);
                    //tracingService.Trace("Subject Name: " + subjectName);

                    DateTime firstDateOfMonth = new DateTime(FromDate.Year, FromDate.Month, 1);
                    DateTime lastDateOfMonth = firstDateOfMonth.AddMonths(1).AddDays(-1);
                    // FetchXML query to retrieve attendance records
                    string fetchXml = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
  <entity name='cdst_studentsattendance'>
    <attribute name='cdst_studentsattendanceid' />
    <attribute name='cdst_name' />
    <attribute name='createdon' />
    <attribute name='cdst_attendace' />
    <attribute name='cdst_class_subject' />
    <order attribute='cdst_name' descending='false' />
    <filter type='and'>
      <condition attribute='cdst_name' operator='eq' value='{studentID}' />
      <condition attribute='createdon' operator='on-or-after' value='{firstDateOfMonth}' />
       <condition attribute='createdon' operator='on-or-before' value='{lastDateOfMonth}' />

    </filter>
    <link-entity name='sms_classsubject' from='sms_classsubjectid' to='cdst_class_subject' link-type='inner' alias='ab'>
        <attribute name='sms_name' alias='ab.sms_name' />
      <filter type='and'>
        <condition attribute='sms_name' operator='not-null' />
      </filter>
    </link-entity>
  </entity>
</fetch>";

                    tracingService.Trace("FetchXML: " + fetchXml);
                    EntityCollection attendances = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace("Number of attendance records retrieved: " + attendances.Entities.Count);

                    // Separate the records into present and absent based on the 'cdst_status' attribute
                    EntityCollection presentStudents = new EntityCollection();
                    EntityCollection absentStudents = new EntityCollection();


                    EntityCollection classSubjects = new EntityCollection();
                    HashSet<Guid> uniqueSubjectIds = new HashSet<Guid>(); // To track uniqueness

                    foreach (Entity attendance in attendances.Entities)
                    {
                        // Make sure the attribute exists and is of type boolean
                        if (attendance.Attributes.Contains("cdst_attendace"))
                        {
                            bool status = attendance.GetAttributeValue<bool>("cdst_attendace");

                            if (status)
                            {
                                presentStudents.Entities.Add(attendance);
                                tracingService.Trace($"Added to PresentStudents: {attendance.Id}");
                            }
                            else
                            {
                                absentStudents.Entities.Add(attendance);
                                tracingService.Trace($"Added to AbsentStudents: {attendance.Id}");
                            }
                        }

                        // Extract class subject and add to collection if not already added
                        if (attendance.Attributes.Contains("cdst_class_subject"))
                        {
                            EntityReference subjectRef = attendance.GetAttributeValue<EntityReference>("cdst_class_subject");
                            if (subjectRef != null && !uniqueSubjectIds.Contains(subjectRef.Id))
                            {
                                uniqueSubjectIds.Add(subjectRef.Id);

                                Entity subjectEntity = new Entity("sms_classsubject");
                                subjectEntity.Id = subjectRef.Id;
                                subjectEntity["sms_name"] = subjectRef.Name;

                                classSubjects.Entities.Add(subjectEntity);
                                tracingService.Trace($"Added subject: {subjectRef.Name} ({subjectRef.Id})");
                            }
                        }
                    }

                    // Output the separated attendance records
                    context.OutputParameters["PresentStudents_attendance"] = presentStudents;
                    context.OutputParameters["AbsentStudents_attendance"] = absentStudents;
                    context.OutputParameters["TotalAttendances_attendance"] = attendances;
                    context.OutputParameters["ClassSubjects_attendance"] = classSubjects;

                }
                catch (Exception ex)
                {
                    tracingService.Trace("subjectAttendanceDetail: {0}", ex.ToString());
                    throw new InvalidPluginExecutionException("An error occurred in subjectAttendanceDetail.", ex);
                }
            }
            else
            {
                tracingService.Trace("subjectAttendanceDetail plugin is not associated with the expected message or is not registered for the main operation.");
            }
        }
    }
}