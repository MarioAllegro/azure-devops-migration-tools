using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class WorkItemPostProcessingContext : MigrationContextBase
    {

        private WorkItemPostProcessingConfig _config;
        private MigrationEngine _me;
        //private IList<string> _workItemTypes;
        //private IList<int> _workItemIDs;
        // private string _queryBit;

        public WorkItemPostProcessingContext(MigrationEngine me, WorkItemPostProcessingConfig config) : base(me, config)
        {
            _me = me;
            _config = config;
        }

        public override string Name
        {
            get
            {
                return "WorkItemPostProcessingContext";
            }
        }
        //public WorkItemPostProcessingContext(MigrationEngine me, WorkItemPostProcessingConfig config, IList<string> wiTypes) : this(me, config)
        //{
        //    _workItemTypes = wiTypes;
        //}

        //public WorkItemPostProcessingContext(MigrationEngine me, WorkItemPostProcessingConfig config, IList<int> wiIDs) : this(me, config)
        //{
        //    _workItemIDs = wiIDs;
        //}

        //public WorkItemPostProcessingContext(MigrationEngine me, WorkItemPostProcessingConfig config, string queryBit) : this (me, config)
        //{
        //    _queryBit = queryBit;
        //}

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
			var sourceStore = new WorkItemStoreContext(me.Source, WorkItemStoreFlags.None);
            var tfsqc = new TfsQueryContext(sourceStore);
            tfsqc.AddParameter("TeamProject", me.Source.Config.Project);

            //Builds the constraint part of the query
            var constraints = BuildQueryBitConstraints();

            tfsqc.Query =
                $@"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {constraints} ORDER BY [System.Id] ";

            var sourceWIS = tfsqc.Execute();
            Trace.WriteLine($"Migrate {sourceWIS.Count} work items?");
            //////////////////////////////////////////////////
            var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            var destProject = targetStore.GetProject();
            Trace.WriteLine($"Found target project as {destProject.Name}");


            var current = sourceWIS.Count;
            var count = 0;
            long elapsedms = 0;
            foreach (WorkItem sourceWI in sourceWIS)
            {
                var witstopwatch = Stopwatch.StartNew();
				WorkItem targetFound;
                targetFound = targetStore.FindReflectedWorkItem(sourceWI, false);
                Trace.WriteLine($"{current} - Updating: {sourceWI.Id}-{sourceWI.Type.Name}");
                if (targetFound == null)
                {
                    Trace.WriteLine(
                        $"{current} - WARNING: does not exist {sourceWI.Id}-{sourceWI.Type.Name}"
                    );
                }
                else
                {
                    Console.WriteLine("...Exists");
                    targetFound.Open();
                    me.ApplyFieldMappings(sourceWI, targetFound);
                    if (targetFound.IsDirty)
                    {
                        try
                        {
                            targetFound.Save();
                            Trace.WriteLine(string.Format("          Updated"));
                        }
                        catch (ValidationException ve)
                        {

                            Trace.WriteLine($"          [FAILED] {ve.ToString()}");
                        }

                    }
                    else
                    {
                        Trace.WriteLine(string.Format("          No changes"));
                    }
                    sourceWI.Close();
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

        private string BuildQueryBitConstraints()
        {
            var constraints = "";

            if (_config.WorkItemIDs != null && _config.WorkItemIDs.Count > 0)
            {
                if (_config.WorkItemIDs.Count == 1)
                {
                    constraints += $" AND [System.Id] = {_config.WorkItemIDs[0]} ";
                }
                else
                {
                    constraints +=
                        $" AND [System.Id] IN ({string.Join(",", _config.WorkItemIDs)}) ";
                }
            }

            if (_me.WorkItemTypeDefinitions != null && _me.WorkItemTypeDefinitions.Count > 0)
            {
                if (_me.WorkItemTypeDefinitions.Count == 1)
                {
                    constraints +=
                        $" AND [System.WorkItemType] = '{_me.WorkItemTypeDefinitions.Keys.First()}' ";
                }
                else
                {
                    constraints +=
                        $" AND [System.WorkItemType] IN ('{string.Join("','", _me.WorkItemTypeDefinitions.Keys)}') ";
                }
            }


            if (!string.IsNullOrEmpty(_config.QueryBit))
            {
                constraints += _config.QueryBit;
            }
            return constraints;
        }
    }
}