using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace neSchedular.schedular
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class JobsController : ControllerBase, IConsumer<ExecuteJobTask>
    {
        readonly ILogger<JobsController> _logger;
        readonly ISchedularService _schedularService;
        readonly IConfiguration _configuration;

        public JobsController(
            ISchedularService schedularService,
            IConfiguration configuration,
            ILogger<JobsController> logger)
        {
            _schedularService = schedularService;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("list")]
        public async Task<JobInfoModel[]> listJobs()
        {
            
            var allJobKeys = await _schedularService.scheduler.GetJobKeys (GroupMatcher<JobKey>.AnyGroup());

            return await Task.WhenAll(allJobKeys.Select(tKey =>
                       JobInfoModel.fromJobKey(_schedularService.scheduler, tKey)));
        }

        IEnumerable<string> jobLogs(JobKey key)
        {
            var fileConfig =  _configuration.GetSection(JobConfigSection.FILELOGGERCONFIGSECTION).Get<filelogger.FileLoggingConfiguration>();
            var logFolderPath = fileConfig.logFileFolder(ScheduledJob.jobNameFromKey(key));

            if (!Directory.Exists(logFolderPath))
            {
                //throw new FileNotFoundException($"folder {logFolderPath} doesn't exist");
                _logger.LogDebug($"folder {logFolderPath} doesn't exist");
                yield break;
            }

            var logFiles = new DirectoryInfo(logFolderPath).GetFiles().OrderByDescending(f => f.CreationTime);

            foreach (var logFile in logFiles)
            {
                /*
                 * var allLines = System.IO.File.ReadAllLines(logFile.FullName);
                 * cannot use this as there will be hsring violation
                 */

                var allLines = new List<string>();
                using (var reader = new StreamReader(System.IO.File.Open(logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    while (reader.Peek() >= 0)
                    {
                        allLines.Add(reader.ReadLine());
                    }
                }

                for (var i = allLines.Count() - 1; i >= 0; i--)
                {
                    yield return allLines[i];
                }
            }
        }

        public async Task Consume(ConsumeContext<ExecuteJobTask> context)
        {
            try
            {
                await _schedularService.RunNowAsync(
                    $"docker.launch.{context.Message.jobName}",
                    new Dictionary<string, object> { { ScheduledJob.INSTANCEPARAM_NAME, context.Message.JobParam } },
                    blockTillComplete:true
                    );
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogCritical(ex, $"message received for non existent job {context.Message.jobName}");
                throw ex;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="runNow"></param>
        /// <param name="jobParam">An extra parameter for the job</param>
        /// <returns></returns>
        [HttpGet("status/{jobName}")]
        public async Task<JobRunningStatusModel> JobStatus(string jobName,[FromQuery] bool runNow = false, [FromQuery] string jobParam = null)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentNullException(nameof(jobName));
            
            var jobData = (runNow && !string.IsNullOrEmpty(jobParam)) ? new Dictionary<string, object> { { ScheduledJob.INSTANCEPARAM_NAME, jobParam } } : null;

            var jobTnfo = runNow? await _schedularService.RunNowAsync(jobName, jobData): await _schedularService.GetJobStatusAsync(jobName);

            return new JobRunningStatusModel
            {
                //currentStatus = await _schedularService.scheduler.GetTriggerState(theTrigger.Key),
                logs = jobLogs(jobTnfo.jobKey).Take(100).ToArray(),
                info = jobTnfo
            };

        }
    }

}
