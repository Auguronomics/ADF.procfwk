using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using Microsoft.Azure.Management.DataFactory;
using Microsoft.Azure.Management.Synapse;

using adf = Microsoft.Azure.Management.DataFactory.Models;
using syn = Microsoft.Azure.Management.Synapse.Models;

using Newtonsoft.Json.Linq;
using ADFprocfwk.Helpers;

namespace ADFprocfwk
{
    public static class CheckPipelineStatus
    {
        [FunctionName("CheckPipelineStatus")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("CheckPipelineStatus Function triggered by HTTP request.");
            
            #region ParseRequestBody
            log.LogInformation("Parsing body from request.");        

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string requestOutputString = string.Empty;

            string tenantId = data?.tenantId;
            string applicationId = data?.applicationId;
            string authenticationKey = data?.authenticationKey;
            string subscriptionId = data?.subscriptionId;
            string resourceGroup = data?.resourceGroup;
            string orchestratorName = data?.orchestratorName;
            string orchestratorType = data?.orchestratorType;
            string pipelineName = data?.pipelineName;
            string runId = data?.runId;

            //Check body for values
            if (
                tenantId == null ||
                applicationId == null ||
                authenticationKey == null ||
                subscriptionId == null ||
                resourceGroup == null ||
                orchestratorName == null ||
                orchestratorType == null ||
                pipelineName == null ||
                runId == null
                )
            {
                log.LogInformation("Invalid body.");
                return new BadRequestObjectResult("Invalid request body, value(s) missing.");
            }
            #endregion

            #region ResolveKeyVaultValues

            log.LogInformation(RequestHelper.CheckGuid(applicationId).ToString());

            if (!RequestHelper.CheckGuid(applicationId) && RequestHelper.CheckUri(applicationId))
            {
                log.LogInformation("Getting applicationId from Key Vault");
                applicationId = KeyVaultClient.GetSecretFromUri(applicationId);
            }

            if (RequestHelper.CheckUri(authenticationKey))
            {
                log.LogInformation("Getting authenticationKey from Key Vault");
                authenticationKey = KeyVaultClient.GetSecretFromUri(authenticationKey);
            }
            #endregion

            #region GetPipelineStatus
            //Create a data factory management client
            log.LogInformation("Creating ADF connectivity client.");
            
            if (orchestratorType.ToUpper() == "ADF")
            {
                using (var client = DataFactoryClient.CreateDataFactoryClient(tenantId, applicationId, authenticationKey, subscriptionId))
                {
                    log.LogInformation("Checking ADF pipeline status.");

                    //Get pipeline status with provided run id
                    adf.PipelineRun pipelineRun;
                    pipelineRun = client.PipelineRuns.Get(resourceGroup, orchestratorName, runId);
                    
                    log.LogInformation("ADF pipeline status: " + pipelineRun.Status);

                    //Final return detail
                    requestOutputString = CreateOutputString(pipelineName, runId, SetSimpleStatus(pipelineRun.Status), pipelineRun.Status);
                }
            }
            else if (orchestratorType.ToUpper() == "SYN")
            {
                using (var client = SynapseClient.CreateSynapseClient(tenantId, applicationId, authenticationKey, subscriptionId))
                {
                    log.LogInformation("Checking SYN pipeline status.");

                    //Get pipeline status with provided run id
                    /*
                    syn.PipelineRun pipelineRun;
                    pipelineRun = client.PipelineRuns.Get(resourceGroup, orchestratorName, runId);

                    log.LogInformation("ADF pipeline status: " + pipelineRun.Status);

                    //Final return detail
                    requestOutputString = CreateOutputString(pipelineName, pipelineRun.RunId, SetSimpleStatus(pipelineRun.Status), pipelineRun.Status);
                    */

                    requestOutputString = CreateOutputString(pipelineName, runId, SetSimpleStatus("Unknown"), "Unknown"); //done just just functions builds
                }
            }
            else
            {
                log.LogInformation("Invalid orchestrator type.");
                return new BadRequestObjectResult("Invalid orchestrator type provided. Expected ADF or SYN.");
            }
            #endregion

            JObject outputJson = JObject.Parse(requestOutputString);

            log.LogInformation("CheckPipelineStatus Function complete.");
            return new OkObjectResult(outputJson);
        }

        #region LocalHelpers

        private static string SetSimpleStatus (string actualStatus)
        {
            string simpleStatus;

            switch (actualStatus)
            {
                case "InProgress":
                    simpleStatus = "Running";
                    break;
                case "Canceling":
                    simpleStatus = "Canceling";
                    break;
                default:
                    simpleStatus = "Done";
                    break;
            }

            return simpleStatus;
        }

        private static string CreateOutputString (string pipelineName, string runId, string simpleStatus, string actualStatus)
        {
            string outputString = "{ \"PipelineName\": \"" + pipelineName +
                                "\", \"RunId\": \"" + runId +
                                "\", \"SimpleStatus\": \"" + simpleStatus +
                                "\", \"Status\": \"" + actualStatus +
                                "\" }";

            return outputString;
        }

        #endregion
    }
}
