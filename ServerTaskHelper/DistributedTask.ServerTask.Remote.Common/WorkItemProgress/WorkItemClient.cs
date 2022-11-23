﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using DistributedTask.ServerTask.Remote.Common.Request;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace DistributedTask.ServerTask.Remote.Common.WorkItemProgress
{
    public class WorkItemClient
    {
        private readonly VssConnection vssConnection;
        private readonly TaskProperties taskProperties;
        private readonly WorkItemTrackingHttpClient witClient;

        public WorkItemClient(TaskProperties taskProperties)
        {
            var vssBasicCredential = new VssBasicCredential(string.Empty, taskProperties.AuthToken);
            vssConnection = new VssConnection(taskProperties.PlanUri, vssBasicCredential);

            this.witClient = vssConnection
                .GetClient<WorkItemTrackingHttpClient>();

            this.taskProperties = taskProperties;
        }

        public WorkItem GetWorkItemById()
        {
            WorkItem wit = null;

            if (taskProperties.MessageProperties.TryGetValue("CommitMessage", out var commitMessage))
            {
                var regex = new Regex(CommitMessageFormat);
                var witIdStr = regex.Match(commitMessage).Groups[1].Value;

                if (Int32.TryParse(witIdStr, out var witId))
                {
                    wit = witClient
                        .GetWorkItemAsync(project: taskProperties.ProjectId.ToString(), id: witId)
                        .Result;
                }
                else
                {
                    throw new Exception("Work item id referenced within the commit message is not a valid integer!\n"
                        + $"Valid format of the commit message: \"{CommitMessageFormat}\"");
                }
            }
            else
            {
                throw new Exception("CommitMessage header is missing from the checks request!");
            }
            return wit;
        }

        public bool IsWorkItemCompleted(WorkItem wit)
        {
            var witType = wit.Fields["System.WorkItemType"].ToString();
            var witStateColors = witClient
                .GetWorkItemTypeStatesAsync(project: taskProperties.ProjectId, type: witType)
                .Result;

            var witState = wit.Fields["System.State"].ToString();
            var witStateCategory = witStateColors
                        .Where(state => state.Name.Equals(witState))
                        .Select(state => state.Category)
                        .First();

            if (witStateCategory.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private const string CommitMessageFormat = @"Work item #([0-9]+) has been linked.";
    }
}
