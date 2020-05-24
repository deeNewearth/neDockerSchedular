﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;


namespace components.schedular
{
    public interface ISchedularService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);

        IScheduler scheduler { get; }

        /// <summary>
        /// Get job info by name and optionally trigger a job to execute right away
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="runNow"></param>
        /// <returns></returns>
        Task<JobInfoModel> GetJobInfoAsync(string jobName, bool runNow = false);
    }

    public class QuartzHostedService : IHostedService
    {
        readonly ISchedularService _schedularService;
        public QuartzHostedService(ISchedularService schedularService)
        {
            _schedularService = schedularService;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _schedularService.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _schedularService.StartAsync(cancellationToken);
        }
    }

    public class SchedularService : ISchedularService
    {
        readonly ISchedulerFactory _schedulerFactory;
        readonly IJobFactory _jobFactory;
        

        IConfiguration _configuration;

        readonly IHostApplicationLifetime _applicationLifetime;

        readonly ILogger _logger;

        public SchedularService(
            IConfiguration configuration,
        ISchedulerFactory schedulerFactory,
        IJobFactory jobFactory,
        IHostApplicationLifetime applicationLifetime,
        ILogger<SchedularService> logger)
        {
            _logger = logger;
            _schedulerFactory = schedulerFactory;
            _jobFactory = jobFactory;
            _applicationLifetime = applicationLifetime;

            _configuration = configuration;


            Microsoft.Extensions.Primitives.ChangeToken.OnChange(() => configuration.GetReloadToken(), () =>
            {
                Task.Run(async () =>
                {
                    await StartAsync(new CancellationTokenSource().Token);
                });
                
            });

        }

        IScheduler _scheduler = null;

        public IScheduler scheduler => _scheduler;


        public async Task<JobInfoModel> GetJobInfoAsync(string jobName, bool runNow = false)
        {
            var allJobs = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            var theJob = allJobs.Where(j => j.Name == jobName).FirstOrDefault();

            if (null == theJob)
                throw new FileNotFoundException($"the job {jobName} not found");

            var jobTnfo = await JobInfoModel.fromJobKey(_scheduler, theJob);

            if (runNow)
            {
                _logger.LogDebug($"runNow needed runningStatus-> {jobTnfo.isRunning}");

                if (jobTnfo.isRunning)
                {
                    throw new Exception("Job is already running");
                }


                await _scheduler.TriggerJob(theJob);

                //give the job a min to Start 
                await Task.Delay(TimeSpan.FromSeconds(5));

                //reload triggers
                jobTnfo = await JobInfoModel.fromJobKey(_scheduler, theJob);

            }

            return jobTnfo;

        }

        //we store the has of the config to avoid un necessary reloads
        string _configHash = null;

        readonly static object _configHashLock = new object();

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var jobsConfig = new JobConfigSection();
                var section = _configuration.GetSection(@"jobsConfig");
                section.Bind(jobsConfig);

                string configHash;
                using (var md5 = MD5.Create())
                {
                    configHash = BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jobsConfig))));
                }

                if (null == _scheduler)
                {
                    _logger.LogInformation("Starting schedular Service");

                    _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                    _scheduler.JobFactory = _jobFactory;
                }
                else
                {
                    lock (_configHashLock)
                    {
                        if (_configHash == configHash)
                        {
                            _logger.LogInformation("Config un changed");
                            return;
                        }
                        else
                        {
                            //we need this in the lock which cannot traverse a wait
                            _configHash = configHash;

                            _logger.LogInformation("Re loading schedular Service");
                        }
                    }

                    await _scheduler.Clear();
                }

                _configHash = configHash;

                if (null == jobsConfig.jobs || jobsConfig.jobs.Keys.Count()== 0)
                {
                    throw new Exception("No jobs found. Please check configuration files");
                }

                await Task.WhenAll(jobsConfig.jobs.Where(j=>!j.Value.disabled).Select(async (jobData) =>
               {
                   var id = $"docker.launch.{jobData.Key}";
                   if (null == jobData.Value.handler)
                   {
                       _logger.LogCritical($"null handler for job: {id}");
                       return;
                   }

                   if (String.IsNullOrWhiteSpace(jobData.Value.cronStatement))
                   {
                       _logger.LogCritical($"null cronStatement for job: {id}");
                       return;
                   }


                   var handlerType = ScheduledJob.mapHandlers[jobData.Value.handler.Value];
                   
                   var job = JobBuilder.Create(handlerType)
                       .WithIdentity(id, handlerType.FullName)
                       .UsingJobData(new JobDataMap(jobData.Value.jobDataMap))
                       .WithDescription(jobData.Value.description ?? "docker job")
                       .Build();

                   var trigger = TriggerBuilder.Create()
                       .WithIdentity($"{id}.trigger", handlerType.FullName)
                       .WithCronSchedule(jobData.Value.cronStatement, x => x.WithMisfireHandlingInstructionDoNothing())
                       .StartNow()
                       .Build();


                   await _scheduler.ScheduleJob(job, trigger, cancellationToken);
               }));

                await _scheduler.Start(cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogCritical($"Failed to Start Schedular service : {ex}");
                await StopAsync(cancellationToken);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _scheduler?.Shutdown(cancellationToken);

            _logger.LogInformation("Schedular Service terminated");

            //shut down the application if this service is gone
            _applicationLifetime.StopApplication();
        }
    }

    public class JobFactory : IJobFactory, IDisposable
    {
        readonly IServiceScope _scope;
        readonly ILogger _logger;
        IHostApplicationLifetime _applicationLifetime;

        public JobFactory(IServiceProvider container,
            IHostApplicationLifetime applicationLifetime,
            ILogger<JobFactory> logger
            )
        {
            _scope = container.CreateScope();
            _logger = logger;
            _applicationLifetime = applicationLifetime;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            var res = _scope.ServiceProvider.GetService(bundle.JobDetail.JobType) as IJob;

            if (null == res)
            {
                _logger.LogCritical($"Failed to create JobObject for {bundle.JobDetail.Key}. Shutting down application");
                _applicationLifetime.StopApplication();
            }

            return res;
        }

        public void ReturnJob(IJob job)
        {
            (job as IDisposable)?.Dispose();
        }

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}
