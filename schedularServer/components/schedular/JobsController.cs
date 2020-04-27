using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace components.schedular
{
    [ApiController]
    [Route("[controller]")]
    public class JobsController : ControllerBase
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
            var fileConfig =  _configuration.GetSection(JobConfigSection.FILELOGGERCONFIGSECTION).Get<schedularServer.FileLoggingConfiguration>();
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

        [HttpGet("status/{jobName}")]
        public async Task<JobRunningStatusModel> JobStatus(string jobName,[FromQuery] bool runNow = false)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentNullException(nameof(jobName));

            var allJobs = await _schedularService.scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            var theJob = allJobs.Where(j => j.Name == jobName).FirstOrDefault();

            if (null == theJob)
                throw new FileNotFoundException($"the job {jobName} not found");


            var jobTnfo = await JobInfoModel.fromJobKey(_schedularService.scheduler, theJob);

            if (runNow)
            {
                _logger.LogDebug($"runNow needed runningStatus-> {jobTnfo.isRunning}");

                if (jobTnfo.isRunning)
                {
                    throw new Exception("Job is already running");
                }


                await _schedularService.scheduler.TriggerJob(theJob);

                //give the job a min to Start 
                await Task.Delay(TimeSpan.FromSeconds(5));

                //reload triggers
                jobTnfo = await JobInfoModel.fromJobKey(_schedularService.scheduler, theJob);

            }

            return new JobRunningStatusModel
            {
                //currentStatus = await _schedularService.scheduler.GetTriggerState(theTrigger.Key),
                logs = jobLogs(theJob).Take(100).ToArray(),
                info = jobTnfo
            };

        }
    }
}
