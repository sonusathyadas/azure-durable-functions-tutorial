using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Threading.Tasks;
using DurableFunctionsDemo.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace DurableFunctionsDemo
{
    public static class OrderProcessorWorkflowFn
    {
        /// <summary>
        /// Http Starter function
        /// </summary>
        /// <param name="req"></param>
        /// <param name="starter"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("OrderProcessor_Starter")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            Order order = await req.Content.ReadAsAsync<Order>();

            string instanceId = await starter.StartNewAsync<Order>("OrderProcessorWorkflowFn", order);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        /// <summary>
        /// Orchestrator function
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        [FunctionName("OrderProcessorWorkflowFn")]
        public static async Task<string> RunOrchestrator([OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            Order order = context.GetInput<Order>();
            var paymentCompleted = await context.CallActivityAsync<bool>("CheckPaymentStatus", order.Id);
            if (paymentCompleted)
            {
                await context.CallActivityAsync("SendOrderToVendorQueue", order);
                await context.CallActivityAsync<bool>("SendConfirmationMail", order);
                return $"Order confirmation mail sent to {order.Email}";
            }
            else
            {
                await context.CallActivityAsync<bool>("SendCancellationMail", order);
                return $"Order is not completed. Cancellation mail sent to {order.Email}";
            }
        }
               
        /// <summary>
        /// Check payment status of the order
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("CheckPaymentStatus")]
        public static async Task<bool> CheckPaymentStatus([ActivityTrigger] int orderId, ILogger log)
        {
            var str = Environment.GetEnvironmentVariable("sqldb_connection");
            using (SqlConnection connection = new SqlConnection(str))
            {
                connection.Open();
                var sql = $"select PaymentStatus from Payments where OrderId={orderId}";
                using(SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    object status= await command.ExecuteScalarAsync();
                    if (status != null)
                    {
                        string statusText = status.ToString();
                        if (statusText == "Completed") return true;
                        else return false;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Send order information to the vendors queue
        /// </summary>
        /// <param name="order"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("SendOrderToVendorQueue")]
        [return: Queue("vendor-orders", Connection = "AzureWebJobsStorage")]
        public static string SendOrderToVendorQueue([ActivityTrigger]Order order, ILogger log)
        {
            return JsonConvert.SerializeObject(order);            
        }

        /// <summary>
        /// Send order confirmation mail
        /// </summary>
        /// <param name="order"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("SendConfirmationMail")]
        public static async Task<bool> SendConfirmationMail([ActivityTrigger] Order order, ILogger log)
        {
            try
            {
                var authKey = Environment.GetEnvironmentVariable("sendgrid_key");
                SendGridClient client = new SendGridClient(authKey);              

                var from = new EmailAddress("sonusathyadas@hotmail.com", "byteSTREAM Admin");
                var subject = $"Your Order confirmed with order Id {order.Id}";
                var to = new EmailAddress(order.Email, order.CustomerName);                
                var htmlContent = $"Hi {order.CustomerName},<br/>" +
                    $"Your order with Id {order.Id} for Rs {order.Amount}/- is confirmed by the seller. Your order will be " +
                    $"delivered on {order.DeliveryDate.ToShortDateString()}. ";
                var message = MailHelper.CreateSingleEmail(from, to, subject,"", htmlContent);
                var response = await client.SendEmailAsync(message);
                return true;
            }catch(Exception ex)
            {
                log.LogInformation(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Send order cancellation mail
        /// </summary>
        /// <param name="order"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("SendCancellationMail")]
        public static async Task<bool> SendCancellationMail([ActivityTrigger] Order order, ILogger log)
        {
            try
            {
                var authKey = Environment.GetEnvironmentVariable("sendgrid_key");
                SendGridClient client = new SendGridClient(authKey);

                var from = new EmailAddress("sonusathyadas@hotmail.com", "byteSTREAM Admin");
                var subject = $"Order cancelled. Order Id: {order.Id}";
                var to = new EmailAddress(order.Email, order.CustomerName);
                var htmlContent = $"Hi {order.CustomerName},<br/>" +
                    $"Your order with Id {order.Id} for Rs {order.Amount}/- is cancelled because the payment is " +
                    $"not completed. You can try to place the order after sometime.";
                var message = MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
                var response = await client.SendEmailAsync(message);

                return true;
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                return false;
            }
        }
    }
}