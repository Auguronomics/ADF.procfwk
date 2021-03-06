﻿using FactoryTesting.Helpers;
using System.Threading.Tasks;

namespace FactoryTesting.Pipelines.Parent
{
    class ParentHelper : CoverageHelper<ParentHelper>
    {
        public async Task RunPipeline()
        {
            await RunPipeline("02-Parent");
        }
        public ParentHelper WithTenantId()
        {
            AddTenantId();
            return this;
        }

        public ParentHelper WithSubscriptionId()
        {
            AddSubscriptionId();
            return this;
        }
        public ParentHelper WithSPNInDatabase(string workerFactoryName)
        {
            AddWorkerSPNStoredInDatabase(workerFactoryName);
            return this;
        }

        public ParentHelper WithSPNInKeyVault(string workerFactoryName)
        {
            AddWorkerSPNStoredInKeyVault(workerFactoryName);
            return this;
        }

        public ParentHelper WithBasicMetadata()
        {
            AddBasicMetadata();
            return this;
        }

        public ParentHelper WithEmptyExecutionTables()
        {
            WithEmptyTable("procfwk.CurrentExecution");
            WithEmptyTable("procfwk.ExecutionLog");
            WithEmptyTable("procfwk.ErrorLog");

            return this;
        }

        public ParentHelper WithSimulatedError()
        {
            SimulateError(true);
            return this;
        }

        public ParentHelper WithoutSimulatedError()
        {
            SimulateError(false);
            return this;
        }

        public ParentHelper WithStagesDisabled()
        {
            EnableDisableMetadata("Stages", false);
            return this;
        }
        
        public ParentHelper WithStagesEnabled()
        {
            EnableDisableMetadata("Stages", true);
            return this;
        }

        public ParentHelper WithPipelinesDisabled()
        {
            EnableDisableMetadata("Pipelines", false);
            return this;
        }
        public ParentHelper WithPipelinesEnabled()
        {
            EnableDisableMetadata("Pipelines", true);
            return this;
        }

        private void EnableDisableMetadata(string table, bool state)
        {
            string paramValue = state ? "true" : "false";
            ExecuteNonQuery(@$"UPDATE [procfwk].[{table}] SET [Enabled] = '{paramValue}'");
        }

        private void SimulateError(bool simulate)
        {
            string paramValue = simulate ? "true" : "false";
            ExecuteNonQuery(@$"UPDATE pp 
SET [ParameterValue] = '{paramValue}' 
FROM [procfwk].[PipelineParameters] pp 
  INNER JOIN  [procfwk].[Pipelines] p ON pp.[PipelineId] = p.[PipelineId] 
WHERE p.[PipelineName] = 'Intentional Error' AND pp.[ParameterName] = 'RaiseErrors'");
        }

        public ParentHelper WithFailureHandling(string mode)
        {
            ExecuteNonQuery(@$"UPDATE [procfwk].[Properties] 
SET [PropertyValue] = '{mode}' 
WHERE [PropertyName] = 'FailureHandling'");
            return this;
        }

        public ParentHelper WithSingleExecutionStage()
        {
            ExecuteNonQuery("UPDATE [procfwk].[Pipelines] SET [StageId] = 1");
            return this;
        }

        public override void TearDown()
        {
            SimulateError(false);  // ensure default behaviour is to *not* simulate errors
            base.TearDown();
        }
    }
}
