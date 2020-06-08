using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Diagnostics;
using VstsSyncMigrator.Engine.ComponentContext;
using System.Linq;
using System.Collections.Generic;
using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class TestVeriablesMigrationContext : MigrationContextBase
    {
        public override string Name
        {
            get { return "TestVeriablesMigrationContext"; }
        }


        // http://blogs.microsoft.co.il/shair/2015/02/02/tfs-api-part-56-test-configurations/

        public TestVeriablesMigrationContext(MigrationEngine me, TestVariablesMigrationConfig config) : base(me, config)
        {

        }

        internal override void InternalExecute()
        {
            WorkItemStoreContext sourceWisc = new WorkItemStoreContext(me.Source, WorkItemStoreFlags.None);
            TestManagementContext SourceTmc = new TestManagementContext(me.Source);

            WorkItemStoreContext targetWisc = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            TestManagementContext targetTmc = new TestManagementContext(me.Target);

            List<ITestVariable> sourceVars = SourceTmc.Project.TestVariables.Query().ToList();
            Trace.WriteLine($"Plan to copy {sourceVars.Count} Veriables?");

            foreach (var sourceVar in sourceVars)
            {
                Trace.WriteLine($"Copy: {sourceVar.Name}");
                ITestVariable targetVar = GetVar(targetTmc.Project.TestVariables, sourceVar.Name);
                if (targetVar == null)
                {
                    Trace.WriteLine($"    Need to create: {sourceVar.Name}");
                    targetVar = targetTmc.Project.TestVariables.Create();
                    targetVar.Name = sourceVar.Name;
                    targetVar.Save();
                }
                else
                {
                    Trace.WriteLine($"    Exists: {sourceVar.Name}");
                }
                // match values
                foreach (var sourceVal in sourceVar.AllowedValues)
                {
                    Trace.WriteLine($"    Seeking: {sourceVal.Value}");
                    ITestVariableValue targetVal = GetVal(targetVar, sourceVal.Value);
                    if (targetVal == null)
                    {
                        Trace.WriteLine($"    Need to create: {sourceVal.Value}");
                        targetVal = targetTmc.Project.TestVariables.CreateVariableValue(sourceVal.Value);
                        targetVar.AllowedValues.Add(targetVal);
                        targetVar.Save();
                    }
                    else
                    {
                        Trace.WriteLine($"    Exists: {targetVal.Value}");
                    }
                }

            }
        }


        internal ITestVariable GetVar(ITestVariableHelper tvh, string variableToFind)
        {
            // Test Variables are case insensitive in VSTS so need ignore case in comparison
            return tvh.Query()
                .FirstOrDefault(variable => string.Equals(variable.Name, variableToFind,
                    StringComparison.OrdinalIgnoreCase));
        }

        internal ITestVariableValue GetVal(ITestVariable targetVar, string valueToFind)
        {
            // Test Variable values are case insensitive in VSTS so need ignore case in comparison
            return targetVar.AllowedValues.FirstOrDefault(
                variable => string.Equals(variable.Value, valueToFind, StringComparison.OrdinalIgnoreCase));
        }

    }
}
