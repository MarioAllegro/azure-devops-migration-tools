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
    public class WorkItemUpdateAreasAsTagsContext : ProcessingContextBase
    {
        private WorkItemUpdateAreasAsTagsConfig config;

        public WorkItemUpdateAreasAsTagsContext(MigrationEngine me, WorkItemUpdateAreasAsTagsConfig config) : base(me, config)
        {
            this.config = config;
        }

        public override string Name
        {
            get
            {
                return "WorkItemUpdateAreasAsTagsContext";
            }
        }

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
			var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);

            var tfsqc = new TfsQueryContext(targetStore);
            tfsqc.AddParameter("TeamProject", me.Target.Config.Project);
            tfsqc.AddParameter("AreaPath", config.AreaIterationPath);
            tfsqc.Query = @"SELECT [System.Id], [System.Tags] FROM WorkItems WHERE  [System.TeamProject] = @TeamProject and [System.AreaPath] under @AreaPath";
            var  workitems = tfsqc.Execute();
            Trace.WriteLine($"Update {workitems.Count} work items?");
            //////////////////////////////////////////////////
            var current = workitems.Count;
            var count = 0;
            long elapsedms = 0;
            foreach (WorkItem workitem in workitems)
            {
                var witstopwatch = Stopwatch.StartNew();

				Trace.WriteLine($"{current} - Updating: {workitem.Id}-{workitem.Type.Name}");
                var areaPath = workitem.AreaPath;
                var bits = new List<string>(areaPath.Split(char.Parse(@"\"))).Skip(4).ToList();
                var tags = workitem.Tags.Split(char.Parse(@";")).ToList();
                var newTags = tags.Union(bits).ToList();
                var newTagList = string.Join(";", newTags.ToArray());
                if (newTagList != workitem.Tags)
                { 
                workitem.Open();
                workitem.Tags = newTagList;
                workitem.Save();

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