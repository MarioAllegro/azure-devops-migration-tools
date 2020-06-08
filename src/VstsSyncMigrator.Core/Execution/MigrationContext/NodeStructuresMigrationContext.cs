using Microsoft.TeamFoundation.Server;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class NodeStructuresMigrationContext : MigrationContextBase
    {
        private NodeStructuresMigrationConfig config;

        public override string Name
        {
            get
            {
                return "NodeStructuresMigrationContext";
            }
        }

        public NodeStructuresMigrationContext(MigrationEngine me, NodeStructuresMigrationConfig config) : base(me, config)
        {
            this.config = config;
        }

        internal override void InternalExecute()
        {
            //////////////////////////////////////////////////
            var sourceCss = (ICommonStructureService)me.Source.Collection.GetService(typeof(ICommonStructureService));
            var sourceProjectInfo = sourceCss.GetProjectFromName(me.Source.Config.Project);
            var sourceNodes = sourceCss.ListStructures(sourceProjectInfo.Uri);
            //////////////////////////////////////////////////
            var targetCss = (ICommonStructureService)me.Target.Collection.GetService(typeof(ICommonStructureService4));

            //////////////////////////////////////////////////
            ProcessCommonStructure("Area", sourceNodes, targetCss, sourceCss);
            //////////////////////////////////////////////////
            ProcessCommonStructure("Iteration", sourceNodes, targetCss, sourceCss);
            //////////////////////////////////////////////////
        }

        private void ProcessCommonStructure(string treeType, NodeInfo[] sourceNodes, ICommonStructureService targetCss, ICommonStructureService sourceCss)
        {
            var sourceNode = (from n in sourceNodes where n.Path.Contains(treeType) select n).Single();
            var sourceTree = sourceCss.GetNodesXml(new string[] { sourceNode.Uri }, true);
            var structureParent = targetCss.GetNodeFromPath(
                $"\\{me.Target.Config.Project}\\{treeType}"
            );
            if (config.PrefixProjectToNodes)
            {
                structureParent = CreateNode(targetCss, me.Source.Config.Project, structureParent);
            }
            if (sourceTree.ChildNodes[0].HasChildNodes)
            {
                CreateNodes(sourceTree.ChildNodes[0].ChildNodes[0].ChildNodes, targetCss, structureParent, treeType);
            }
        }

        private void CreateNodes(XmlNodeList nodeList, ICommonStructureService css, NodeInfo parentPath, string treeType)
        {
            foreach (XmlNode item in nodeList)
            {
                var newNodeName = item.Attributes["Name"].Value;

                if (!ShouldCreateNode(parentPath, newNodeName))
                {
                    continue;
                }

                NodeInfo targetNode;
                if (treeType == "Iteration")
                {
                    DateTime? startDate = null;
                    DateTime? finishDate = null;
                    if (item.Attributes["StartDate"] != null)
                    {
                        startDate = DateTime.Parse(item.Attributes["StartDate"].Value);
                    }
                    if (item.Attributes["FinishDate"] != null)
                    {
                        finishDate = DateTime.Parse(item.Attributes["FinishDate"].Value);
                    }
                    
                    targetNode = CreateNode(css, newNodeName, parentPath, startDate, finishDate);
                }
                else
                {
                    targetNode = CreateNode(css, newNodeName, parentPath);
                }
                if (item.HasChildNodes)
                {
                    CreateNodes(item.ChildNodes[0].ChildNodes, css, targetNode, treeType);
                }
            }
        }

        /// <summary>
        /// Checks node-to-be-created with allowed BasePath's
        /// </summary>
        /// <param name="parentPath">Parent Node</param>
        /// <param name="newNodeName">Node to be created</param>
        /// <returns>true/false</returns>
        private bool ShouldCreateNode(NodeInfo parentPath, string newNodeName)
        {
            var nodePath = $@"{parentPath.Path}\{newNodeName}";

            if (config.BasePaths != null && config.BasePaths.Any())
            {
                var split = nodePath.Split('\\');
                var removeProjectAndType = split.Skip(3);
                var path = string.Join(@"\", removeProjectAndType);
                
                // We need to check if the path is a parent path of one of the base paths, as we need those
                foreach (var basePath in config.BasePaths)
                {
                    var splitBase = basePath.Split('\\');

                    for (var i = 0; i < splitBase.Length; i++)
                    {
                        if (string.Equals(path, string.Join(@"\", splitBase.Take(i)), StringComparison.InvariantCultureIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                if (!config.BasePaths.Any(p => path.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                {
                    Trace.WriteLine($"--IgnoreNode: {nodePath}");
                    return false;
                }
            }

            return true;
        }

        private NodeInfo CreateNode(ICommonStructureService css, string name, NodeInfo parent)
        {
            var nodePath = $@"{parent.Path}\{name}";
            NodeInfo node = null;
            
            Trace.Write($"--CreateNode: {nodePath}");
            try
            {
                node = css.GetNodeFromPath(nodePath);
                Trace.Write("...found");
            }
            catch (CommonStructureSubsystemException ex)
            {
                Telemetry.Current.TrackException(ex);
                Trace.Write("...missing");
                var newPathUri = css.CreateNode(name, parent.Uri);
                Trace.Write("...created");
                node = css.GetNode(newPathUri);
            }

            Trace.WriteLine(string.Empty);
            return node;
        }

        private NodeInfo CreateNode(ICommonStructureService css, string name, NodeInfo parent, DateTime? startDate, DateTime? finishDate)
        {
            var nodePath = $@"{parent.Path}\{name}";
            NodeInfo node = null;
            Trace.Write(
                $"--CreateNode: {nodePath}, start date: {startDate}, finish date: {finishDate}"
            );
            try
            {
                node = css.GetNodeFromPath(nodePath);
                Trace.Write("...found");
            }
            catch (CommonStructureSubsystemException ex)
            {
                Telemetry.Current.TrackException(ex);
                Trace.Write("...missing");
                var newPathUri = css.CreateNode(name, parent.Uri);
                Trace.Write("...created");
                node = css.GetNode(newPathUri);
                ((ICommonStructureService4)css).SetIterationDates(node.Uri, startDate, finishDate);
                Trace.Write("...dates assigned");
            }

            Trace.WriteLine(string.Empty);
            return node;
        }
    }
}