using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;

namespace Student_Payment
{
    public class Invoice : IPlugin
    {
        // Helper method for downloading file and converting to Base64
        private string DownloadFileAndGetBase64(IOrganizationService service, string entityName, Guid recordGuid, string fileAttributeName)
        {
            try
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
                    FileContinuationToken = initializeFileBlocksDownloadResponse.FileContinuationToken,
                    BlockLength = initializeFileBlocksDownloadResponse.FileSizeInBytes
                };

                var downloadBlockResponse = (DownloadBlockResponse)service.Execute(downloadBlockRequest);
                byte[] fileBytes = downloadBlockResponse.Data;
                return Convert.ToBase64String(fileBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException($"Error downloading file: {ex.Message}", ex);
            }
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            if (context.MessageName.Equals("dst_student_payments"))
            {
                try
                {
                    tracingService.Trace("Plugin execution started.");

                    // Validate input parameter
                    if (!context.InputParameters.Contains("studentID_payment") ||
                        string.IsNullOrEmpty(context.InputParameters["studentID_payment"]?.ToString()))
                    {
                        throw new InvalidPluginExecutionException("studentID_payment parameter is required.");
                    }

                    string studentID = context.InputParameters["studentID_payment"].ToString();
                    tracingService.Trace($"Student ID: {studentID}");

                    // Define file fields to be processed
                    List<string> fileFieldNames = new List<string> { "cdst_voucher" };

                    // Retrieve payments related to the student
                    string fetchXml = $@"
                        <fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                            <entity name='cdst_payment'>
                                <attribute name='cdst_paymentid' />
                                <attribute name='cdst_name' />
                                <attribute name='createdon' />
                                <attribute name='cdst_paymentstatus' />
                                <attribute name='cdst_invoiceid' />
                                <attribute name='cdst_duedate' />
                                <attribute name='cdst_voucher' />
                                <order attribute='cdst_name' descending='false' />
                                <link-entity name='contact' from='contactid' to='cdst_student' link-type='inner' alias='aa'>
                                    <attribute name='entityimage' />
                                    <filter type='and'>
                                        <condition attribute='cdst_studentid' operator='eq' value='{studentID}' />
                                    </filter>
                                </link-entity>
                            </entity>
                        </fetch>";

                    EntityCollection payments = service.RetrieveMultiple(new FetchExpression(fetchXml));
                    tracingService.Trace($"Number of payment records retrieved: {payments.Entities.Count}");

                    // Create a list to hold our processed payment data
                    var processedPayments = new List<Entity>();

                    // Process each payment
                    foreach (var entity in payments.Entities)
                    {
                        // Create a new entity to hold our processed data
                        var processedPayment = new Entity(entity.LogicalName);
                        processedPayment.Id = entity.Id;

                        // Copy all attributes except file fields
                        foreach (var attribute in entity.Attributes)
                        {
                            if (!fileFieldNames.Contains(attribute.Key))
                            {
                                processedPayment[attribute.Key] = attribute.Value;
                            }
                        }

                        // Process file fields
                        foreach (var fileField in fileFieldNames)
                        {
                            if (entity.Contains(fileField))
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
                                        // Store base64 in a temporary attribute
                                        processedPayment[$"temp_{fileField}_base64"] = base64Content;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tracingService.Trace($"Error downloading file for field '{fileField}' on entity {entity.Id}: {ex}");
                                    processedPayment[$"temp_{fileField}_error"] = ex.Message;
                                }
                            }
                        }

                        processedPayments.Add(processedPayment);
                    }

                    // Create a response entity collection
                    var responseCollection = new EntityCollection(processedPayments);
                    responseCollection.EntityName = payments.EntityName;
                    responseCollection.MoreRecords = payments.MoreRecords;
                    responseCollection.PagingCookie = payments.PagingCookie;
                    responseCollection.TotalRecordCount = payments.TotalRecordCount;
                    responseCollection.TotalRecordCountLimitExceeded = payments.TotalRecordCountLimitExceeded;

                    // Output the result
                    context.OutputParameters["Payments_ResponseAPI"] = responseCollection;
                    tracingService.Trace("Plugin execution completed successfully.");
                }
                catch (Exception ex)
                {
                    tracingService.Trace($"Error: {ex}");
                    throw new InvalidPluginExecutionException($"An error occurred in payments Dynamic API: {ex.Message}", ex);
                }
            }
        }
    }
}