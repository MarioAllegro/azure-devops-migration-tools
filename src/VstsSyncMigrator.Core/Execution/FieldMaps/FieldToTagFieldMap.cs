using System;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using VstsSyncMigrator.Engine.ComponentContext;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VstsSyncMigrator.Engine.Configuration.FieldMap;

namespace VstsSyncMigrator.Engine
{
    public class FieldToTagFieldMap : IFieldMap
    {
        private FieldtoTagMapConfig config;

        public FieldToTagFieldMap(FieldtoTagMapConfig config)
        {
            this.config = config;
        }

        public string Name
        {
            get
            {
                return "FieldToTagFieldMap";
            }
        }

        public string MappingDisplayName => config.sourceField;

        public void Execute(WorkItem source, WorkItem target)
        {
            if (source.Fields.Contains(this.config.sourceField))
            {
                List<string> newTags = target.Tags.Split(char.Parse(@";")).ToList();
                // to tag
                if (source.Fields[this.config.sourceField].Value != null)
                {
                    string value = source.Fields[this.config.sourceField].Value.ToString();
                    if (string.IsNullOrEmpty(value))
                    {
                        if (string.IsNullOrEmpty(config.formatExpression))
                        {
                            newTags.Add(value);
                        }
                        else
                        {
                            newTags.Add(string.Format(config.formatExpression, value));
                        }
                        target.Tags = string.Join(";", newTags.ToArray());
                        Trace.WriteLine(
                            $"  [UPDATE] field tagged {source.Id}:{this.config.sourceField} to {target.Id}:Tag with foramt of {config.formatExpression}"
                        );
                    }

                }

            }
        }
    }
}