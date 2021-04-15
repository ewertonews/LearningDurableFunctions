using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PocDurableFunctions
{
    public class Status
    {
        public List<string> Messages { get; set; } = new List<string>();
    }
    public static class FunctionsOrchestrator
    {
        [FunctionName("FunctionsOrchestrator")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {

            var status = new Status();
            status.Messages.Add("Started saying hellos");
            var outputs = new List<string>();
            
            context.SetCustomStatus(status);

            outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Tokyo"));
            status.Messages.Add("Said hello to Tokio");
            context.SetCustomStatus(status);

            outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Seattle"));
            status.Messages.Add("Said hello to Seattle");
            status.Messages.Add("Awaiting external event to say hello to London");

            context.SetCustomStatus(status);

            await context.WaitForExternalEvent("FinishedHello");

            outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "London"));
            context.SetCustomStatus("Finished hellos");
            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("Function1_Hello")]
        public static string SayHello([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Saying hello to {name}.");
            Thread.Sleep(10000);
            return $"Hello {name}!";
        }

        [FunctionName("Function1_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("FunctionsOrchestrator", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("RaiseEventToOrchestration")]
        public static async Task Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sendinteraction/{instanceId}")] HttpRequestMessage req,        
        [DurableClient] IDurableOrchestrationClient client,
        string instanceId)
        {
            await client.RaiseEventAsync(instanceId, "FinishedHello");           
        }
    }
}