using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class WorkItemDelete : ProcessingContextBase
    {


        public WorkItemDelete(MigrationEngine me, ITfsProcessingConfig config) : base(me, config)
        {

        }

        public override string Name
        {
            get
            {
                return "WorkItemDelete";
            }
        }

        internal override void InternalExecute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
			WorkItemStoreContext targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            TfsQueryContext tfsqc = new TfsQueryContext(targetStore);
            tfsqc.AddParameter("TeamProject", me.Target.Config.Project);
            tfsqc.Query =
                $@"SELECT [System.Id] FROM WorkItems WHERE  [System.TeamProject] = @TeamProject  AND [System.AreaPath] UNDER '{me.Target.Config.Project}\_DeleteMe'";
            WorkItemCollection  workitems = tfsqc.Execute();
            Trace.WriteLine($"Update {workitems.Count} work items?");
            //////////////////////////////////////////////////
            int current = workitems.Count;
            //int count = 0;
            //long elapsedms = 0;
            var tobegone = (from WorkItem wi in workitems where wi.AreaPath.Contains("_DeleteMe")  select wi.Id).ToList();

            foreach (int begone in tobegone)
            {
                targetStore.Store.DestroyWorkItems(new List<int>() { begone });
                Trace.WriteLine($"Deleted {begone}");
            }

            
            //////////////////////////////////////////////////
            stopwatch.Stop();
            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }

    }
}