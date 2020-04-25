using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using Quartz.Spi;

namespace components.schedular
{
    /// <summary>
    /// Quartz job to launch a docker container
    /// </summary>
    [DisallowConcurrentExecution]
    public class DockerRunService : IJob
    {
        readonly ILogger _logger;
        IConfiguration _configuration;

        public DockerRunService(
            IConfiguration configuration,
            ILogger<DockerRunService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Used to store Run information
        /// </summary>
        class RunData
        {
            public DateTime startTime { get; set; }
            public DateTime endTime { get; set; }
        }

        string _logFolder = null;
        string _slackKey = null;
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                if(null == _slackKey)
                {
                    _slackKey = _configuration["notifications:slack"];
                    Debug.Assert(!string.IsNullOrWhiteSpace(_slackKey), "Put your slack key in global environment  notifications__slack for slack publish to work");
                }


                if (null == _logFolder)
                {
                    _logFolder = _configuration["Docker:logFolder"];
                    if (string.IsNullOrWhiteSpace(_logFolder))
                        _logFolder = Path.GetTempPath();

                    _logFolder = Path.Combine(_logFolder, "dockerRunLogs");

                    if (!Directory.Exists(_logFolder))
                    {
                        Directory.CreateDirectory(_logFolder);
                    }

                    _logger.LogInformation($"using _logFolder -> {_logFolder}");
                }


                _logger.LogInformation($"{DateTime.UtcNow} : {context.JobDetail.Key} :Starting");

                var launchParamsKey = context.JobDetail.JobDataMap[JobScheduleModel.LAUNCH_PARAMS_CONFIG_KEY] as string;
                if (string.IsNullOrWhiteSpace(launchParamsKey))
                    throw new Exception($"No launch Parameters for job ");


                var launchConfig = new DockerRunParamsModel();
                _configuration.GetSection(launchParamsKey).Bind(launchConfig);

                if (string.IsNullOrWhiteSpace(launchConfig.containerId))
                    throw new Exception($"No containerId for launchConfig ");


                var dockerUrl = _configuration["Docker:uri"];
                if (string.IsNullOrWhiteSpace(dockerUrl))
                    throw new Exception("Docker:uri is empty");


                var client = new DockerClientConfiguration(new Uri(dockerUrl)).CreateClient();


                /* the best way is to deploy container manually and use this to run
                var found = await client.Images.ListImagesAsync(new ImagesListParameters
                {
                    MatchName = launchConfig.image
                });

               

                if (found.Count > 1)
                {
                    throw new Exception($"multiple images found for {launchConfig.image}" );
                }else if(0== found.Count)
                {
                    await client.Images.CreateImageAsync(new ImagesCreateParameters
                    {
                        FromImage = launchConfig.image
                    }, null, new Progress<JSONMessage>(p =>
                    {
                        _logger.LogInformation($"{p.Time}: [{p.Status}: {p.ProgressMessage}]");
                        if (!string.IsNullOrWhiteSpace(p.ErrorMessage))
                            _logger.LogError(p.ErrorMessage);
                    }));
                }
                

                var container = await client.Containers.CreateContainerAsync(new CreateContainerParameters
                {
                    Image = launchConfig.image,
                    
                });

               */

                var started = DateTime.Now;
                if (!await client.Containers.StartContainerAsync(launchConfig.containerId, new ContainerStartParameters { }))
                    throw new Exception("failed to start container");

                //The warning is incorrect
#pragma warning disable CS0618 // Type or member is obsolete
                await Task.WhenAll(Task.Run(async () =>
                {
                    var done = await client.Containers.WaitContainerAsync(launchConfig.containerId);
                    _logger.LogInformation($"{DateTime.UtcNow} : {context.JobDetail.Key} :done -> {done.StatusCode}");

                    var doneData = JsonConvert.SerializeObject(new
                    {
                        started,
                        completed = DateTime.Now,
                        done.StatusCode
                    });

                    var keyEnd = context.JobDetail.Key.Name.Split('.').Last();

                    var statFilePre = Path.Combine(_logFolder, "${keyEnd}_" + DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss"));
                    var statFile = $"{statFilePre}.log";
                    var i = 1;
                    while (File.Exists(statFile))
                    {
                        statFile = $"{statFilePre}_{i++}.log";
                    }

                    await File.WriteAllTextAsync(statFile, $"{doneData}\n");

                    if (0 != done.StatusCode)
                    {
                        throw new Exception($"Run failed with Status:{done.StatusCode}");
                    }
                }),
                client.Containers.GetContainerLogsAsync(launchConfig.containerId, new ContainerLogsParameters
                {
                    Follow = true,
                    ShowStdout = true,
                    ShowStderr = true,
                    Tail = "200"
                }, default, new Progress<string>(log => _logger.LogInformation(log)))
                    );

#pragma warning restore CS0618 // Type or member is obsolete

            }
            catch (Exception ex)
            {
                _logger.LogCritical($"Failed job execution {context.JobDetail.Key} -> {ex}");

                /*
                bool ranOnce = _statData.ContainsKey(context.JobDetail.Key) && _statData[context.JobDetail.Key].ranOnce;

                if (!ranOnce)
                {
                    _logger.LogCritical($"Stoping all triggers for {context.JobDetail.Key}");
                }*/

                throw new JobExecutionException($"Failed job execution {context.JobDetail.Key}", ex, false)
                {
                    // UnscheduleAllTriggers = !ranOnce
                };
            }
            
        }
    }

    


}
