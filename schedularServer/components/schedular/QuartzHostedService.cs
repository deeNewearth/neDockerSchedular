using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Quartz.Listener;
using Quartz.Spi;


namespace neSchedular.schedular
{
    public interface ISchedularService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);

        IScheduler scheduler { get; }

        Task<JobInfoModel> GetJobStatusAsync(string jobName);


        /// <summary>
        /// triggers a job to execute right away
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="instanceDataMap">use this exta data for run now</param>
        /// <returns></returns>
        Task<JobInfoModel> RunNowAsync(string jobName,
            IDictionary<string, object> instanceDataMap = null,
            bool blockTillComplete = false,
            TimeSpan? timeout = null
            );
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

        readonly MyJobListener _jobListener;

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

            _jobListener = new MyJobListener(_logger);

            Microsoft.Extensions.Primitives.ChangeToken.OnChange(() => configuration.GetReloadToken(), () =>
            {
                Task.Run(async () =>
                {
                    await StartAsync(new CancellationTokenSource().Token);
                });

            });

        }

        public IScheduler scheduler { get; private set; } = null;


        public async Task<JobInfoModel> RunNowAsync(string jobName,
            IDictionary<string, object> instanceDataMap = null,
            bool blockTillComplete = false,
            TimeSpan? timeout = null
            )
        {
            var jobTnfo = await GetJobStatusAsync(jobName);

            _logger.LogDebug($"runNow needed runningStatus-> {jobTnfo.isRunning}");

            if (jobTnfo.isRunning)
            {
                var ex = new Exception("Job is already running");
                _logger.LogCritical(ex, "Job is already running");
                throw ex;
            }

            var myInstanceMap = new Dictionary<string, Object>(instanceDataMap);
            myInstanceMap[JobInfoModel.INTANCE_GUID_NAME] = Guid.NewGuid();

            var taskWaiter = blockTillComplete ?
                _jobListener.getJobWaiter((Guid)myInstanceMap[JobInfoModel.INTANCE_GUID_NAME]) : null;
            

            await scheduler.TriggerJob(jobTnfo.jobKey, new JobDataMap(myInstanceMap as IDictionary<string, object>));

            if (blockTillComplete)
            {
                var done = await Task.WhenAny(taskWaiter, Task.Delay(-1, new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(15)).Token));
                if (done.IsFaulted)
                {
                    throw done.Exception.InnerException ?? done.Exception;
                }
            }
            else
            {
                //give the job a min to Start 
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            //reload triggers
            jobTnfo = await JobInfoModel.fromJobKey(scheduler, jobTnfo.jobKey);


            return jobTnfo;

        }

        public async Task<JobInfoModel> GetJobStatusAsync(string jobName)
        {
            var allJobs = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            var theJob = allJobs.Where(j => j.Name == jobName).FirstOrDefault();

            if (null == theJob)
                throw new FileNotFoundException($"the job {jobName} not found");

            return  await JobInfoModel.fromJobKey(scheduler, theJob);
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

                if (null == scheduler)
                {
                    _logger.LogInformation("Starting schedular Service");

                    scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                    scheduler.JobFactory = _jobFactory;
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

                    await scheduler.Clear();
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


                   await scheduler.ScheduleJob(job, trigger, cancellationToken);
               }));


                scheduler.ListenerManager.AddJobListener(_jobListener, GroupMatcher<JobKey>.AnyGroup());

                await scheduler.Start(cancellationToken);
            }
            catch(Exception ex)
            {
                _logger.LogCritical($"Failed to Start Schedular service : {ex}");
                await StopAsync(cancellationToken);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await scheduler?.Shutdown(cancellationToken);

            _logger.LogInformation("Schedular Service terminated");

            //shut down the application if this service is gone
            _applicationLifetime.StopApplication();
        }
    }

    public class MyJobListener : JobListenerSupport
    {
        public override string Name => "PrimaryJobListener";

        readonly ConcurrentDictionary<Guid, TaskCompletionSource<object>> _mapJobCompletion = new ConcurrentDictionary<Guid, TaskCompletionSource<object>>();

        ILogger _logger;

        public MyJobListener(ILogger logger)
        {
            _logger = logger;
        }

        public Task<object> getJobWaiter(Guid instanceId)
        {
            var theTaskSource =  _mapJobCompletion.GetOrAdd(instanceId,  new TaskCompletionSource<object>());

            return theTaskSource.Task;

        }

        public override Task JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException, CancellationToken cancellationToken = default)
        {
            if(!context.Trigger.JobDataMap.ContainsKey(JobInfoModel.INTANCE_GUID_NAME))
            {
                Debug.Assert(false, "Should never happen");
                _logger.LogWarning("We have task without JobInfoModel.INTANCE_GUID_NAME");
                return Task.CompletedTask;
            }

            var taskId = (Guid)context.Trigger.JobDataMap[JobInfoModel.INTANCE_GUID_NAME];

            TaskCompletionSource<object> complitionSource;
            if (_mapJobCompletion.TryRemove((Guid)context.Trigger.JobDataMap[JobInfoModel.INTANCE_GUID_NAME],out complitionSource))
            {
                if(null == jobException)
                {
                    complitionSource.SetResult(context.Result);
                }
                else
                {
                    complitionSource.SetException(jobException);
                }
                
            }
            else
            {
                //we have no one to talk to
                _logger.LogDebug($"No waiters for the Task Instance {taskId}");
            }

            return Task.CompletedTask;
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
