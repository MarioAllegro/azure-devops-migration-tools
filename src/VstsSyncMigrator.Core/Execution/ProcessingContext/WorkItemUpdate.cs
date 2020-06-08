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
    public class WorkItemUpdate : ProcessingContextBase
    {
        private WorkItemUpdateConfig _config;
        private MigrationEngine _me;

        public WorkItemUpdate(MigrationEngine me, WorkItemUpdateConfig config) : base(me, config)
        {
            _me = me;
            _config = config;
        }

        public override string Name
        {
            get
            {
                return "WorkItemUpdate";
            }
        }

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
			var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);

            var tfsqc = new TfsQueryContext(targetStore);
            tfsqc.AddParameter("TeamProject", me.Target.Config.Project);
            tfsqc.Query =
                $@"SELECT [System.Id], [System.Tags] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {_config.QueryBit} ORDER BY [System.ChangedDate] desc";
            var  workitems = tfsqc.Execute();
            Trace.WriteLine($"Update {workitems.Count} work items?");
            //////////////////////////////////////////////////
            var current = workitems.Count;
            var count = 0;
            long elapsedms = 0;
            foreach (WorkItem workitem in workitems)
            {
                var witstopwatch = Stopwatch.StartNew();
				workitem.Open();
                Trace.WriteLine(
                    $"Processing work item {workitem.Id} - Type:{workitem.Type.Name} - ChangedDate:{workitem.ChangedDate.ToShortDateString()} - CreatedDate:{workitem.CreatedDate.ToShortDateString()}"
                );
                _me.ApplyFieldMappings(workitem);

                if (workitem.IsDirty)
                {
                    if (!_config.WhatIf)
                    {
                        try
                        {
                            workitem.Save();
                        }
                        catch (Exception)
                        {
                            System.Threading.Thread.Sleep(5000);
                            workitem.Save();
                        }
                       
                    } else
                    {
                        Trace.WriteLine("No save done: (What IF: enabled)");
                    }
                    
                } else
                {
                    Trace.WriteLine("No save done: (IsDirty: false)");
                }
                



                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                var average = new TimeSpan(0, 0, 0, 0, (int)(elapsedms / count));
                var remaining = new TimeSpan(0, 0, 0, 0, (int)(average.TotalMilliseconds * current));
                Trace.WriteLine(
                    $"Average time of {$@"{average:s\:fff} seconds"} per work item and {string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)} estimated to completion"
                );
            }
            //////////////////////////////////////////////////
            stopwatch.Stop();
            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }

    }
}