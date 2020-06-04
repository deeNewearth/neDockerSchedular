using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace neSchedular.schedular
{
   



    [DisallowConcurrentExecution]
    public class DockerStartJob : ScheduledJob, IJob {
        public DockerStartJob(
            docker.IDockerExecuter docker,
            ILogger<ScheduledJob> logger) 
            : base(async (jobName, instanceParam, token)=> { await docker.StartContainerAsync(jobName, instanceParam, logger, token); return null; }, logger) { }
    }


    [DisallowConcurrentExecution]
    public class DockerExecJob : ScheduledJob, IJob
    {
        public DockerExecJob(
            docker.IDockerExecuter docker,
            ILogger<ScheduledJob> logger)
            : base(async (jobName, instanceParam, token) => { await docker.ExecContainerAsync(jobName, instanceParam, logger, token); return null; }, logger) { }
    }


    [DisallowConcurrentExecution]
    public class ScheduledJob : IJob
    {
        public static readonly IReadOnlyDictionary<JobHandlerEnumModel, Type> mapHandlers = new Dictionary<JobHandlerEnumModel, Type>
        {
            { JobHandlerEnumModel.start,typeof(DockerStartJob)},
            { JobHandlerEnumModel.exec,typeof(DockerExecJob)},
           /* { JobHandlerEnumModel.run,typeof(DockerRunJob)},  WE might now ever want a RUN Job
            * It's better to set things up with compose, test them and just use exec if we need parameters  or start we none needed
            */
        };

        /// <summary>
        /// Note that Information level will be sent to Slack so watch out what we log, and is logged and used 
        /// also we need to make sure to follow the conventions so that data is saved as Json properly
        /// </summary>
        readonly ILogger<ScheduledJob> _logger;

        /// <summary>
        /// parameters: jobName, instanceParam (extra param for the trigger), Returns an object with ToString overwritten.. for UI display
        /// </summary>
        Func<string,string, CancellationToken, Task<object>> _executer;

        public ScheduledJob(
            Func<string, string, CancellationToken, Task<object>> executer,
            ILogger<ScheduledJob> logger)
        {
            _logger = logger;
            _executer = executer;
        }

        public static string jobNameFromKey(JobKey key)
        {
            return key.Name.Split('.').Last();
        }

        public readonly static string INSTANCEPARAM_NAME = "instanceParam";

        public async Task Execute(IJobExecutionContext context)
        {
            var jobName = jobNameFromKey( context.JobDetail.Key);
            var started = DateTime.UtcNow;

            object jobResult = null;
            try
            {
                _logger.LogDebug(new EventId(0, "jobStarted"), "{jobName} ->Starting", jobName);

                var timeOut = context.JobDetail.JobDataMap.ContainsKey(JobScheduleModel.TIMEOUT) ?
                    TimeSpan.Parse((string)context.JobDetail.JobDataMap[JobScheduleModel.TIMEOUT]) : TimeSpan.FromHours(1);

                string instanceParam = null;
                if (null != context.Trigger.JobDataMap && context.Trigger.JobDataMap.ContainsKey(INSTANCEPARAM_NAME))
                {
                    instanceParam = context.Trigger.JobDataMap[INSTANCEPARAM_NAME] as string;
                }

                var src = new CancellationTokenSource(timeOut);
                jobResult = await _executer(jobName, instanceParam, src.Token);

                var logString = "The task *{jobName}* started at {started} UTC and ran for {duration}";
                var logParams = new object[] { jobName, started, DateTime.UtcNow - started };
                if (null != jobResult)
                {
                    logString += " and returned {jobResult}";
                    logParams = logParams.Concat(new[] { jobResult.ToString() }).ToArray();
                }

                _logger.LogInformation(new EventId(0, "jobFinished"), logString, logParams);


            }
            catch (OperationCanceledException ex)
            {
                _logger.LogCritical(new EventId(0, "timeout"), ex, "The task *{jobName}* started at {started} UTC and has been running for {duration}. It seems hung",
                    jobName, started, DateTime.UtcNow - started);

                throw new JobExecutionException($"Failed job execution {jobName}", ex, false)
                {
                    // UnscheduleAllTriggers = !ranOnce
                };
            }
            catch (Exception ex)
            {
                _logger.LogCritical(new EventId(0, "exception"), ex, "The task *{jobName}* Failed", jobName);



                throw new JobExecutionException($"Failed job execution {jobName}", ex, false)
                {
                    // UnscheduleAllTriggers = !ranOnce
                };

            }
            finally {
                //This would be an exception or result for the Job watchers
                context.Result = jobResult;
            }
        }
    }


   

}
