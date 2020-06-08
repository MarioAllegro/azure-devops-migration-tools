using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.ProcessConfiguration.Client;
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
    public class TeamMigrationContext : MigrationContextBase
    {
        private TeamMigrationConfig _config;
        private MigrationEngine _me;

        public override string Name
        {
            get
            {
                return "TeamMigrationContext";
            }
        }

        public TeamMigrationContext(MigrationEngine me, TeamMigrationConfig config) : base(me, config)
        {
            _me = me;
            _config = config;
        }

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
            //////////////////////////////////////////////////
            var sourceStore = new WorkItemStoreContext(me.Source, WorkItemStoreFlags.BypassRules);
            var sourceTS = me.Source.Collection.GetService<TfsTeamService>();
            var sourceTL = sourceTS.QueryTeams(me.Source.Config.Project).ToList();
            Trace.WriteLine($"Found {sourceTL.Count} teams in Source?");
            var sourceTSCS = me.Source.Collection.GetService<TeamSettingsConfigurationService>();
            //////////////////////////////////////////////////
            var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            var targetProject = targetStore.GetProject();
            Trace.WriteLine($"Found target project as {targetProject.Name}");
            var targetTS = me.Target.Collection.GetService<TfsTeamService>();
            var targetTL = targetTS.QueryTeams(me.Target.Config.Project).ToList();
            Trace.WriteLine($"Found {targetTL.Count} teams in Target?");
            var targetTSCS = me.Target.Collection.GetService<TeamSettingsConfigurationService>();
            //////////////////////////////////////////////////
            var current = sourceTL.Count;
            var count = 0;
            long elapsedms = 0;

            /// Create teams
            /// 
            foreach (var sourceTeam in sourceTL)
            {
                var witstopwatch = Stopwatch.StartNew();
                var foundTargetTeam = (from x in targetTL where x.Name == sourceTeam.Name select x).SingleOrDefault();
                if (foundTargetTeam == null)
                {
                    Trace.WriteLine($"Processing team '{sourceTeam.Name}':");
                    var newTeam = targetTS.CreateTeam(targetProject.Uri.ToString(), sourceTeam.Name, sourceTeam.Description, null);
                    Trace.WriteLine($"-> Team '{sourceTeam.Name}' created");

                    if (_config.EnableTeamSettingsMigration)
                    {
                        /// Duplicate settings
                        Trace.WriteLine($"-> Processing team '{sourceTeam.Name}' settings:");
                        var sourceConfigurations = sourceTSCS.GetTeamConfigurations(new List<Guid> { sourceTeam.Identity.TeamFoundationId });
                        var targetConfigurations = targetTSCS.GetTeamConfigurations(new List<Guid> { newTeam.Identity.TeamFoundationId });

                        foreach (var sourceConfig in sourceConfigurations)
                        {
                            var targetConfig = targetConfigurations.FirstOrDefault(t => t.TeamName == sourceConfig.TeamName);
                            if (targetConfig == null)
                            {
                                Trace.WriteLine(
                                    $"-> Settings for team '{sourceTeam.Name}'.. not found"
                                );
                                continue;
                            }

                            Trace.WriteLine($"-> Settings found for team '{sourceTeam.Name}'..");
                            if (_config.PrefixProjectToNodes)
                            {
                                targetConfig.TeamSettings.BacklogIterationPath =
                                    $"{me.Target.Config.Project}\\{sourceConfig.TeamSettings.BacklogIterationPath}";
                                targetConfig.TeamSettings.IterationPaths = sourceConfig.TeamSettings.IterationPaths
                                    .Select(path => $"{me.Target.Config.Project}\\{path}")
                                    .ToArray();
                                targetConfig.TeamSettings.TeamFieldValues = sourceConfig.TeamSettings.TeamFieldValues
                                    .Select(field => new TeamFieldValue
                                    {
                                        IncludeChildren = field.IncludeChildren,
                                        Value = $"{me.Target.Config.Project}\\{field.Value}"
                                    })
                                    .ToArray();
                            }
                            else
                            {
                                targetConfig.TeamSettings.BacklogIterationPath = sourceConfig.TeamSettings.BacklogIterationPath;
                                targetConfig.TeamSettings.IterationPaths = sourceConfig.TeamSettings.IterationPaths;
                                targetConfig.TeamSettings.TeamFieldValues = sourceConfig.TeamSettings.TeamFieldValues;
                            }

                            targetTSCS.SetTeamSettings(targetConfig.TeamId, targetConfig.TeamSettings);
                            Trace.WriteLine(
                                $"-> Team '{targetConfig.TeamName}' settings... applied"
                            );
                        }
                    }
                }
                else
                {
                    Trace.WriteLine($"Team '{sourceTeam.Name}' found.. skipping");
                }

                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                var average = new TimeSpan(0, 0, 0, 0, (int)(elapsedms / count));
                var remaining = new TimeSpan(0, 0, 0, 0, (int)(average.TotalMilliseconds * current));
                Trace.WriteLine("");
                //Trace.WriteLine(string.Format("Average time of {0} per work item and {1} estimated to completion", string.Format(@"{0:s\:fff} seconds", average), string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)));
            }
            // Set Team Settings
            //foreach (TeamFoundationTeam sourceTeam in sourceTL)
            //{
            //    Stopwatch witstopwatch = new Stopwatch();
            //    witstopwatch.Start();
            //    var foundTargetTeam = (from x in targetTL where x.Name == sourceTeam.Name select x).SingleOrDefault();
            //    if (foundTargetTeam == null)
            //    {
            //        Trace.WriteLine(string.Format("Processing team {0}", sourceTeam.Name));
            //        var sourceTCfU = sourceTSCS.GetTeamConfigurations((new[] { sourceTeam.Identity.TeamFoundationId })).SingleOrDefault();
            //        TeamSettings newTeamSettings = CreateTargetTeamSettings(sourceTCfU);
            //        TeamFoundationTeam newTeam = targetTS.CreateTeam(targetProject.Uri.ToString(), sourceTeam.Name, sourceTeam.Description, null);
            //        targetTSCS.SetTeamSettings(newTeam.Identity.TeamFoundationId, newTeamSettings);
            //    }
            //    else
            //    {
            //        Trace.WriteLine(string.Format("Team found.. skipping"));
            //    }

            //    witstopwatch.Stop();
            //    elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
            //    current--;
            //    count++;
            //    TimeSpan average = new TimeSpan(0, 0, 0, 0, (int)(elapsedms / count));
            //    TimeSpan remaining = new TimeSpan(0, 0, 0, 0, (int)(average.TotalMilliseconds * current));
            //    Trace.WriteLine("");
            //    //Trace.WriteLine(string.Format("Average time of {0} per work item and {1} estimated to completion", string.Format(@"{0:s\:fff} seconds", average), string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)));

            //}
            //////////////////////////////////////////////////
            stopwatch.Stop();
            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }


        private TeamSettings CreateTargetTeamSettings(TeamConfiguration sourceTCfU)
        {
            ///////////////////////////////////////////////////
            var newTeamSettings = sourceTCfU.TeamSettings;
            newTeamSettings.BacklogIterationPath = newTeamSettings.BacklogIterationPath.Replace(me.Source.Config.Project, me.Target.Config.Project);
            var newIterationPaths = new List<string>();
            foreach (var ip in newTeamSettings.IterationPaths)
            {
                newIterationPaths.Add(ip.Replace(me.Source.Config.Project, me.Target.Config.Project));
            }
            newTeamSettings.IterationPaths = newIterationPaths.ToArray();

            ///////////////////////////////////////////////////
            return newTeamSettings;
        }
    }
}