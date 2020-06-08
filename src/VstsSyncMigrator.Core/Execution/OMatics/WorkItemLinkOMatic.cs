using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VstsSyncMigrator.Engine;
using VstsSyncMigrator.Engine.Execution.Exceptions;

namespace VstsSyncMigrator.Core.Execution.OMatics
{
    public class WorkItemLinkOMatic
    {

        public void MigrateLinks(WorkItem sourceWorkItemLinkStart, WorkItemStoreContext sourceWorkItemStore, WorkItem targetWorkItemLinkStart, WorkItemStoreContext targetWorkItemStore, bool save = true)
        {
            if (targetWorkItemLinkStart.Links.Count == sourceWorkItemLinkStart.Links.Count)
            {
                Trace.WriteLine(
                    $"[SKIP] Source and Target have same number of links  {sourceWorkItemLinkStart.Id} - {sourceWorkItemLinkStart.Type.ToString()}", "LinkMigrationContext");
            }
            else
            {

                Trace.Indent();
                foreach (Link item in sourceWorkItemLinkStart.Links)
                {
                    try
                    {
                        Trace.WriteLine(
                            $"Migrating link for {sourceWorkItemLinkStart.Id} of type {item.GetType().Name}", "LinkMigrationContext");
                        if (IsHyperlink(item))
                        {
                            CreateHyperlink((Hyperlink)item, targetWorkItemLinkStart, save);
                        }
                        else if (IsRelatedLink(item))
                        {
                            RelatedLink rl = (RelatedLink)item;
                            CreateRelatedLink(sourceWorkItemLinkStart, rl, targetWorkItemLinkStart, sourceWorkItemStore, targetWorkItemStore, save);
                        }
                        else if (IsExternalLink(item))
                        {
                            ExternalLink rl = (ExternalLink)item;
                            CreateExternalLink((ExternalLink)item, targetWorkItemLinkStart, save);
                        }
                        else
                        {
                            UnknownLinkTypeException ex = new UnknownLinkTypeException(
                                $"  [UnknownLinkType] Unable to {item.GetType().Name}"
                            );
                            Telemetry.Current.TrackException(ex);
                            Trace.WriteLine(ex.ToString(), "LinkMigrationContext");
                            throw ex;
                        }
                    }
                    catch (WorkItemLinkValidationException ex)
                    {
                        sourceWorkItemLinkStart.Reset();
                        targetWorkItemLinkStart.Reset();
                        Telemetry.Current.TrackException(ex);
                        Trace.WriteLine(
                            $"  [WorkItemLinkValidationException] Adding link for wiSourceL={sourceWorkItemLinkStart.Id}", "LinkMigrationContext");
                        Trace.WriteLine(ex.ToString(), "LinkMigrationContext");
                    }
                    catch (FormatException ex)
                    {
                        sourceWorkItemLinkStart.Reset();
                        targetWorkItemLinkStart.Reset();
                        Telemetry.Current.TrackException(ex);
                        Trace.WriteLine(
                            $"  [CREATE-FAIL] Adding Link for wiSourceL={sourceWorkItemLinkStart.Id}", "LinkMigrationContext");
                        Trace.WriteLine(ex.ToString(), "LinkMigrationContext");
                    }
                }

            }
            if (sourceWorkItemLinkStart.Type.Name == "Test Case")
            {
                MigrateSharedSteps(sourceWorkItemLinkStart, targetWorkItemLinkStart, sourceWorkItemStore, targetWorkItemStore, save);
            }
        }

        private void MigrateSharedSteps(WorkItem wiSourceL, WorkItem wiTargetL, WorkItemStoreContext sourceStore,
            WorkItemStoreContext targetStore, bool save)
        {
            const string microsoftVstsTcmSteps = "Microsoft.VSTS.TCM.Steps";
            var oldSteps = wiTargetL.Fields[microsoftVstsTcmSteps].Value.ToString();
            var newSteps = oldSteps;

            var sourceSharedStepLinks = wiSourceL.Links.OfType<RelatedLink>()
                .Where(x => x.LinkTypeEnd.Name == "Shared Steps").ToList();
            var sourceSharedSteps =
                sourceSharedStepLinks.Select(x => sourceStore.Store.GetWorkItem(x.RelatedWorkItemId));

            foreach (WorkItem sourceSharedStep in sourceSharedSteps)
            {
                WorkItem matchingTargetSharedStep =
                    targetStore.FindReflectedWorkItemByReflectedWorkItemId(sourceSharedStep);

                if (matchingTargetSharedStep != null)
                {
                    newSteps = newSteps.Replace($"ref=\"{sourceSharedStep.Id}\"",
                        $"ref=\"{matchingTargetSharedStep.Id}\"");
                    wiTargetL.Fields[microsoftVstsTcmSteps].Value = newSteps;
                }
            }

            if (wiTargetL.IsDirty && save)
            {
                wiTargetL.Fields["System.ChangedBy"].Value = "Migration";
                wiTargetL.Save();
            }
        }

        private void CreateExternalLink(ExternalLink sourceLink, WorkItem target, bool save )
        {
            var exist = (from Link l in target.Links
                         where l is ExternalLink && ((ExternalLink)l).LinkedArtifactUri == ((ExternalLink)sourceLink).LinkedArtifactUri
                         select (ExternalLink)l).SingleOrDefault();
            if (exist == null)
            {

                Trace.WriteLine($"Creating new {sourceLink.GetType().Name} on {target.Id}", "LinkMigrationContext");
                ExternalLink el = new ExternalLink(sourceLink.ArtifactLinkType, sourceLink.LinkedArtifactUri);
                el.Comment = sourceLink.Comment;
                target.Links.Add(el);
                if (save)
                {
                    target.Fields["System.ChangedBy"].Value = "Migration";
                    target.Save();
                }
            }
            else
            {
                Trace.WriteLine($"Link {sourceLink.GetType().Name} on {target.Id} already exists", "LinkMigrationContext");
            }
        }

        private bool IsExternalLink(Link item)
        {
            return item is ExternalLink;
        }

        private void CreateRelatedLink(WorkItem wiSourceL, RelatedLink item, WorkItem wiTargetL, WorkItemStoreContext sourceStore, WorkItemStoreContext targetStore, bool save )
        {
            RelatedLink rl = (RelatedLink)item;
            WorkItem wiSourceR = null;
            WorkItem wiTargetR = null;
            try
            {
                wiSourceR = sourceStore.Store.GetWorkItem(rl.RelatedWorkItemId);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"  [FIND-FAIL] Adding Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiTargetL={wiTargetL.Id} "
                );
                Trace.TraceError(ex.ToString());
                return;
            }
            try
            {
                wiTargetR = GetRightHandSideTargitWi(wiSourceL, wiSourceR, wiTargetL, targetStore);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(
                    $"  [FIND-FAIL] Adding Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiTargetL={wiTargetL.Id} "
                );
                Trace.TraceError(ex.ToString());
                return;

            }
            if (wiTargetR != null)
            {
                bool IsExisting = false;
                try
                {
                    var exist = (
                        from Link l in wiTargetL.Links
                        where l is RelatedLink
                            && ((RelatedLink)l).RelatedWorkItemId == wiTargetR.Id
                            && ((RelatedLink)l).LinkTypeEnd.ImmutableName == item.LinkTypeEnd.ImmutableName
                        select (RelatedLink)l).SingleOrDefault();
                    IsExisting = (exist != null);
                }
                catch (Exception ex)
                {

                    Trace.WriteLine(
                        $"  [SKIP] Unable to migrate links where wiSourceL={((wiSourceL != null) ? wiSourceL.Id.ToString() : "NotFound")}, wiSourceR={((wiSourceR != null) ? wiSourceR.Id.ToString() : "NotFound")}, wiTargetL={((wiTargetL != null) ? wiTargetL.Id.ToString() : "NotFound")}"
                    );
                    Trace.TraceError(ex.ToString());
                    return;
                }

                if (!IsExisting && !wiTargetR.IsAccessDenied)
                {

                    if (wiSourceR.Id != wiTargetR.Id)
                    {
                        Trace.WriteLine(
                            $"  [CREATE-START] Adding Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiSourceR={wiSourceR.Id}, wiTargetL={wiTargetL.Id}, wiTargetR={wiTargetR.Id} "
                        );
                        WorkItemLinkTypeEnd linkTypeEnd = targetStore.Store.WorkItemLinkTypes.LinkTypeEnds[rl.LinkTypeEnd.ImmutableName];
                        RelatedLink newRl = new RelatedLink(linkTypeEnd, wiTargetR.Id);

                        wiTargetL.Links.Add(newRl);
                        if (save)
                        {
                            wiTargetL.Fields["System.ChangedBy"].Value = "Migration";
                            wiTargetL.Save();
                        }
                        Trace.WriteLine(
                            $"  [CREATE-SUCCESS] Adding Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiSourceR={wiSourceR.Id}, wiTargetL={wiTargetL.Id}, wiTargetR={wiTargetR.Id} "
                        );
                    }
                    else
                    {
                        Trace.WriteLine(
                            $"  [SKIP] Unable to migrate link where Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiSourceR={wiSourceR.Id}, wiTargetL={wiTargetL.Id}, wiTargetR={wiTargetR.Id} as target WI has not been migrated"
                        );
                    }
                }
                else
                {
                    if (IsExisting)
                    {
                        Trace.WriteLine(
                            $"  [SKIP] Already Exists a Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiSourceR={wiSourceR.Id}, wiTargetL={wiTargetL.Id}, wiTargetR={wiTargetR.Id} "
                        );
                    }
                    if (wiTargetR.IsAccessDenied)
                    {
                        Trace.WriteLine(
                            $"  [AccessDenied] The Target  work item is inaccessable to create a Link of type {rl.LinkTypeEnd.ImmutableName} where wiSourceL={wiSourceL.Id}, wiSourceR={wiSourceR.Id}, wiTargetL={wiTargetL.Id}, wiTargetR={wiTargetR.Id} "
                        );
                    }
                }

            }
            else
            {
                Trace.WriteLine(
                    $"  [SKIP] Cant find wiTargetR where wiSourceL={wiSourceL.Id}, wiSourceR={wiSourceR.Id}, wiTargetL={wiTargetL.Id}"
                );
            }


        }

        private WorkItem GetRightHandSideTargitWi(WorkItem wiSourceL, WorkItem wiSourceR, WorkItem wiTargetL, WorkItemStoreContext targetStore)
        {
            WorkItem wiTargetR;
            if (!(wiTargetL == null)
                && wiSourceR.Project.Name == wiTargetL.Project.Name
                && wiSourceR.Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", "") == wiTargetL.Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", ""))
            {
                // Moving to same team project as SourceR
                wiTargetR = wiSourceR;
            }
            else
            {
                // Moving to Other Team Project from Source
                wiTargetR = targetStore.FindReflectedWorkItem(wiSourceR, true);
                if (wiTargetR == null) // Assume source only (other team project)
                {
                    wiTargetR = wiSourceR;
                    if (wiTargetR.Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", "") != wiSourceR.Project.Store.TeamProjectCollection.Uri.ToString().Replace("/", ""))
                    {
                        wiTargetR = null; // Totally bogus break! as not same team collection
                    }
                }
            }
            return wiTargetR;
        }

        private bool IsRelatedLink(Link item)
        {
            return item is RelatedLink;
        }

        private void CreateHyperlink(Hyperlink sourceLink, WorkItem target, bool save )
        {
            var exist = (from Link l in target.Links where l is Hyperlink && ((Hyperlink)l).Location == ((Hyperlink)sourceLink).Location select (Hyperlink)l).SingleOrDefault();
            if (exist == null)
            {
                Hyperlink hl = new Hyperlink(sourceLink.Location);
                hl.Comment = sourceLink.Comment;
                target.Links.Add(hl);
                if (save)
                {
                    target.Fields["System.ChangedBy"].Value = "Migration";
                    target.Save();
                }                
            }
        }

        private bool IsHyperlink(Link item)
        {
            return item is Hyperlink;
        }
    }

}
