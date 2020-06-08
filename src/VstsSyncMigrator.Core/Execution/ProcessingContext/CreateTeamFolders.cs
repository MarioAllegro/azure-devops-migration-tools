using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.TeamFoundation.Client;
using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class CreateTeamFolders : ProcessingContextBase
    {


        public CreateTeamFolders(MigrationEngine me, ITfsProcessingConfig config) : base(me, config)
        {
         
        }

        public override string Name
        {
            get
            {
                return "CreateTeamFolders";
            }
        }

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
			var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);

            var tfsqc = new TfsQueryContext(targetStore);

            var teamService = me.Target.Collection.GetService<TfsTeamService>();
            var qh = targetStore.Store.Projects[me.Target.Config.Project].QueryHierarchy;
            var teamList = teamService.QueryTeams(me.Target.Config.Project).ToList();

            Trace.WriteLine($"Found {teamList.Count} teams?");
            //////////////////////////////////////////////////
            var current = teamList.Count;
            var count = 0;
            long elapsedms = 0;
            foreach (var team in teamList)
            {
                var witstopwatch = Stopwatch.StartNew();

				Trace.Write($"Processing team {team.Name}");
                var r = new Regex(@"^Project - ([a-zA-Z ]*)");
                string path;
                if (r.IsMatch(team.Name))
                {
                    Trace.Write(string.Format(" is a Project"));
                    path = $@"Projects\{r.Match(team.Name).Groups[1].Value.Replace(" ", "-")}";

                }
                else
                {
                    Trace.Write(string.Format(" is a Team"));
                    path = $@"Teams\{team.Name.Replace(" ", "-")}";
                }
                Trace.Write($" and new path is {path}");
                //me.AddFieldMap("*", new RegexFieldMap("KM.Simulation.Team", "System.AreaPath", @"^Project - ([a-zA-Z ]*)", @"Nemo\Projects\$1"));

                var bits = path.Split(char.Parse(@"\"));

                CreateFolderHyerarchy(bits, qh["Shared Queries"]);

                //_me.ApplyFieldMappings(workitem);
                qh.Save();


                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                var average = new TimeSpan(0, 0, 0, 0, (int)(elapsedms / count));
                var remaining = new TimeSpan(0, 0, 0, 0, (int)(average.TotalMilliseconds * current));
                Trace.WriteLine("");
                //Trace.WriteLine(string.Format("Average time of {0} per work item and {1} estimated to completion", string.Format(@"{0:s\:fff} seconds", average), string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)));
            }
            //////////////////////////////////////////////////
            stopwatch.Stop();
            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }


        private void CreateFolderHyerarchy(string[] toCreate, QueryItem currentItem, int focus = 0)
        {
            if (currentItem is QueryFolder)
            {
                var currentFolder = (QueryFolder)currentItem;
                
                if (!currentFolder.Contains(toCreate[focus]))
                {
                    currentFolder.Add(new QueryFolder(toCreate[focus]));
                    Trace.WriteLine($"  Created: {toCreate[focus]}");
                }
                if (toCreate.Length != focus+1)
                {
                    CreateFolderHyerarchy(toCreate, currentFolder[toCreate[focus]], focus + 1);
                }
            }
        }
    }
}
