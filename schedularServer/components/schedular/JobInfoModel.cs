using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace components.schedular
{
    /// <summary>
    /// used to send Job info over the wire
    /// </summary>
    public class JobInfoModel
    {
        public string jobName { get; set; }
        public DateTimeOffset? previousFired { get; set; }
        public DateTimeOffset? nextScheduled { get; set; }

        public bool isRunning { get; set; }

        public string cronSummary { get; set; }

        public JobInfoModel() { }

        public static async Task<JobInfoModel> fromJobKey(IScheduler scheduler, JobKey theJob)
        {
            var triggers = await scheduler.GetTriggersOfJob(theJob);

            if (triggers.Count() == 0)
                throw new System.IO.FileNotFoundException($"the job {theJob.Name} has no triggers");

            return await fromTriggers(scheduler, triggers);
        }


        public static async Task<JobInfoModel> fromTriggers(IScheduler scheduler, IEnumerable<ITrigger> triggers)
        {
            var status =await Task.WhenAll( triggers.Select(async trigger => new
            {
                trigger,
                cronSummary = trigger is ICronTrigger? ((ICronTrigger)trigger).GetExpressionSummary():null,
                status = await scheduler.GetTriggerState(trigger.Key),
                prevFired = trigger.GetPreviousFireTimeUtc(),
                nextFired = trigger.GetNextFireTimeUtc()
            }));

            return new JobInfoModel
            {
                jobName = status[0].trigger.JobKey.Name,
                cronSummary = status.Where(s => !string.IsNullOrWhiteSpace(s.cronSummary)).Select(s=>s.cronSummary).FirstOrDefault(),
                isRunning = status.Where(s=>s.status == TriggerState.Blocked).Count() > 1,
                previousFired = status.Where(s => s.prevFired.HasValue).OrderByDescending(s => s.prevFired.Value).Select(s => s.prevFired.Value).FirstOrDefault(),
                nextScheduled = status.Where(s => s.nextFired.HasValue).OrderBy(s => s.nextFired.Value).Select(s => s.nextFired.Value).FirstOrDefault(),
            };

        }

    }
}
