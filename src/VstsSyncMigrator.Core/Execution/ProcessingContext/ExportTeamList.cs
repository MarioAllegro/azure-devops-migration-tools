using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Common;
using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class ExportTeamList : ProcessingContextBase
    {


        public ExportTeamList(MigrationEngine me, ITfsProcessingConfig config) : base(me, config)
        {

        }

        public override string Name
        {
            get
            {
                return "ExportTeamList";
            }
        }

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
			//////////////////////////////////////////////////
			// Retrieve the project URI. Needed to enumerate teams.     
			var css4 = me.Target.Collection.GetService<ICommonStructureService4>();
            var projectInfo = css4.GetProjectFromName(me.Target.Config.Project);
            // Retrieve a list of all teams on the project.     
            var teamService = me.Target.Collection.GetService<TfsTeamService>();

            foreach (var p in css4.ListAllProjects())
            {
                var allTeams = teamService.QueryTeams(p.Uri);

                foreach (var team in allTeams)
                {
                    Trace.WriteLine($"Team name: {team.Name}", p.Name);
                    Trace.WriteLine($"Team ID: {team.Identity.TeamFoundationId.ToString()}", p.Name);
                    Trace.WriteLine($"Description: {team.Description}", p.Name);
                    var members =  team.GetMembers(me.Target.Collection, MembershipQuery.Direct);
                    Trace.WriteLine(
                        $"Team Accounts: {string.Join(";", (from member in team.GetMembers(me.Target.Collection, MembershipQuery.Direct) select member.UniqueName))}", p.Name);
                    Trace.WriteLine(
                        $"Team names: {string.Join(";", (from member in team.GetMembers(me.Target.Collection, MembershipQuery.Direct) select member.DisplayName))}", p.Name);
                }
            }

           




            //////////////////////////////////////////////////
            stopwatch.Stop();

            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }

    }
}