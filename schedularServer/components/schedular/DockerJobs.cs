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

namespace components.schedular
{
    [DisallowConcurrentExecution]
    public class DockerStartJob : ScheduledJob, IJob {
        public DockerStartJob(
            docker.IDockerExecuter docker,
            ILogger<ScheduledJob> logger) 
            : base((jobName, token)=> docker.StartContainerAsync(jobName, logger, token), logger) { }
    }


    [DisallowConcurrentExecution]
    public class DockerExecJob : ScheduledJob, IJob
    {
        public DockerExecJob(
            docker.IDockerExecuter docker,
            ILogger<ScheduledJob> logger)
            : base((jobName, token) => docker.ExecContainerAsync(jobName, logger, token), logger) { }
    }


    [DisallowConcurrentExecution]
    public class ScheduledJob : IJob
    {
        public static readonly IReadOnlyDictionary<JobHandlerEnumModel, Type> mapHandlers = new Dictionary<JobHandlerEnumModel, Type>
        {
            { JobHandlerEnumModel.start,typeof(DockerStartJob)},
            { JobHandlerEnumModel.exec,typeof(DockerExecJob)}
        };

        /// <summary>
        /// Note that Information level will be sent to Slack so watch out what we log, and is logged and used 
        /// also we need to make sure to follow the conventions so that data is saved as Json properly
        /// </summary>
        readonly ILogger<ScheduledJob> _logger;
        
        Func<string, CancellationToken, Task> _executer;

        public ScheduledJob(
            Func<string, CancellationToken, Task> executer,
            ILogger<ScheduledJob> logger)
        {
            _logger = logger;
            _executer = executer;
        }

        public static string jobNameFromKey(JobKey key)
        {
            return key.Name.Split('.').Last();
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobName = jobNameFromKey( context.JobDetail.Key);
            var started = DateTime.UtcNow;
            try
            {
                _logger.LogDebug(new EventId(0, "jobStarted"), "{jobName} ->Starting", jobName);

                var timeOut = context.JobDetail.JobDataMap.ContainsKey(JobScheduleModel.TIMEOUT) ?
                    TimeSpan.Parse((string)context.JobDetail.JobDataMap[JobScheduleModel.TIMEOUT]) : TimeSpan.FromHours(1);

                var src = new CancellationTokenSource(timeOut);
                await _executer(jobName, src.Token);

                _logger.LogInformation(new EventId(0, "jobFinished"), "The task *{jobName}* started at {started} UTC and ran for {duration}", jobName, started, DateTime.UtcNow - started);


            }
            catch (OperationCanceledException ex)
            {
                _logger.LogCritical(new EventId(0, "timeout"),ex, "The task *{jobName}* started at {started} UTC and has been running for {duration}. It seems hung",
                    jobName, started, DateTime.UtcNow - started);
                
                throw new JobExecutionException($"Failed job execution {jobName}", ex, false)
                {
                    // UnscheduleAllTriggers = !ranOnce
                };
            }
            catch (Exception ex)
            {
                _logger.LogCritical(new EventId(0, "exception"), ex, "The task *{jobName}* Failed", jobName);

                /*
                bool ranOnce = _statData.ContainsKey(context.JobDetail.Key) && _statData[context.JobDetail.Key].ranOnce;

                if (!ranOnce)
                {
                    _logger.LogCritical($"Stoping all triggers for {context.JobDetail.Key}");
                }*/

                throw new JobExecutionException($"Failed job execution {jobName}", ex, false)
                {
                    // UnscheduleAllTriggers = !ranOnce
                };

            }
        }
    }


   

}
