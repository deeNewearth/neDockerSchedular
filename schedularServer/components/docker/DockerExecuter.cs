using Docker.DotNet;
using Docker.DotNet.Models;
using MassTransit.ConsumerSpecifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.AdoJobStore.Common;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace neSchedular.docker
{
    public interface IDockerExecuter
    {
        Task RunContainerAsync(string jobName, string instanceParam, ILogger logger, CancellationToken cancellationToken = default(CancellationToken));
        Task StartContainerAsync(string jobName, string instanceParam, ILogger logger, CancellationToken cancellationToken = default(CancellationToken));
        Task ExecContainerAsync(string jobName, string instanceParam, ILogger logger, CancellationToken cancellationToken = default(CancellationToken));
    }

    public class DockerExecuter: IDockerExecuter
    {
        IConfiguration _configuration;

        public DockerExecuter(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        
        T readParameters<T>(string jobName) where T : new()
        {
            var launchParamsKey = $"jobsConfig:jobs:{jobName}:parameters";
            if (string.IsNullOrWhiteSpace(launchParamsKey))
                throw new Exception($"No launch Parameters for job ");

            var launchConfig = new T();
            _configuration.GetSection(launchParamsKey).Bind(launchConfig);

            return launchConfig;
        }

        DockerClient createDockerClient()
        {
            var dockerUrl = _configuration["Docker:uri"];
            if (string.IsNullOrWhiteSpace(dockerUrl))
                throw new Exception("Docker:uri is empty");

            return new DockerClientConfiguration(new Uri(dockerUrl)).CreateClient();
        }

        public async Task ExecContainerAsync(string jobName, string instanceParam, ILogger logger, CancellationToken cancellationToken = default(CancellationToken))
        {
            var execConfig = readParameters<DockerExecParamsModel>(jobName);

            if (string.IsNullOrWhiteSpace(execConfig.containerId))
                throw new Exception($"No containerId for launchConfig ");

            if (null == execConfig.commands || execConfig.commands.Length ==0 ||
                string.IsNullOrWhiteSpace(execConfig.commands[0]))
                throw new Exception($"No command for launchConfig ");


            var client = createDockerClient();

            var cmd = execConfig.commands;
            if (!string.IsNullOrEmpty(instanceParam))
            {
                cmd = cmd.Concat(new[] { instanceParam }).ToArray();
            }

            ///
            if (!execConfig.runNakedCommand)
            {
                cmd = new[] { "bash", "-c", $"if {string.Join(" ", cmd)}; then echo all done; else echo NESCHEDULAR_COMMAND_FAILED; fi" };
            }

            logger.LogDebug("Running command " + string.Join(" ", cmd));

            ContainerExecCreateResponse created;
            var createParams = new ContainerExecCreateParameters
            {
                AttachStderr = true,
                AttachStdin = true,
                AttachStdout = true,
                Cmd = cmd,
                Detach = false,
                Tty = false
            };

            try
            {
                created = await client.Containers.ExecCreateContainerAsync(execConfig.containerId, createParams);

            }
            catch(DockerApiException ex)
            {
                logger.LogInformation(ex, $"container :{execConfig.containerId} is not running. We will attempt to start it");

                if(!await client.Containers.StartContainerAsync(execConfig.containerId, new ContainerStartParameters { }, cancellationToken))
                throw new Exception("failed to start container");

                created = await client.Containers.ExecCreateContainerAsync(execConfig.containerId, createParams);

            }

            using (var containerStream = await client.Containers.StartAndAttachContainerExecAsync(created.ID,false,cancellationToken ))
            {
                var buffer = new byte[1024];
                bool isError = false;
                var currentOutputString = string.Empty;
                var containerLogs = string.Empty;
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Array.Clear(buffer, 0, buffer.Length);

                    var result = await containerStream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);

                    var output = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    containerLogs += output;

                    //seems like we don't get error messages in all cases
                    if (output.Contains(@" exec failed") || output.Contains(@"NESCHEDULAR_COMMAND_FAILED"))
                    {
                        isError = true;
                    }

                    var splitted = (currentOutputString + output).Split('\n');

                    //sometime buffer returns in mid string. we always want to LOg complete strings
                    var toLog = string.Join("\n", splitted.SkipLast(1)).Trim();
                    if (!string.IsNullOrWhiteSpace(toLog))
                    {
                        logger.LogDebug(new EventId(0, "output"), "{jobName} ->{log}", jobName, toLog);
                    }

                    currentOutputString = splitted.Last();

                    /* There are all sort of scripts that write to stdErr even if success
                    if (null == errorString && MultiplexedStream.TargetStream.StandardError == result.Target)
                    {
                        errorString = output;
                    }
                    */

                    if (result.EOF)
                    {
                        if (!string.IsNullOrWhiteSpace(currentOutputString))
                        {
                            logger.LogDebug(new EventId(0, "output"), "{jobName} ->{log}", jobName, currentOutputString);
                        }
                        break;
                    }

                }

                if (isError)
                {
                    throw new Exception("Run failed with error :\n"+ containerLogs);
                }


            }


        }

        public async Task RunContainerAsync(string jobName, string instanceParam, ILogger logger, CancellationToken cancellationToken = default(CancellationToken))
        {
            var launchConfig = readParameters<RunParamsModel>(jobName);

            if (string.IsNullOrWhiteSpace(launchConfig.image))
                throw new Exception($"No image for launchConfig ");


            var client = createDockerClient();
            
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
                    logger.LogInformation($"{p.Time}: [{p.Status}: {p.ProgressMessage}]");
                    if (!string.IsNullOrWhiteSpace(p.ErrorMessage))
                        logger.LogError(p.ErrorMessage);
                }));
            }

            var options = new CreateContainerParameters
            {
                Image = launchConfig.image,
            };

            if (null != launchConfig.commands && launchConfig.commands.Length > 0)
                options.Cmd = launchConfig.commands;

            if (null != launchConfig.entryPoint && launchConfig.entryPoint.Length > 0)
                options.Entrypoint = launchConfig.entryPoint;

            if (null != launchConfig.env && launchConfig.env.Length > 0)
                options.Env= launchConfig.env;

            var container = await client.Containers.CreateContainerAsync(options);

            logger.LogInformation($"Container created from image {launchConfig.image}");

            try
            {
                var started = DateTime.UtcNow;
                if (!await client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters { }, cancellationToken))
                    throw new Exception("failed to start container");

                //The warning is incorrect
#pragma warning disable CS0618 // Type or member is obsolete
                await Task.WhenAll(Task.Run(async () =>
                {
                    var done = await client.Containers.WaitContainerAsync(container.ID, cancellationToken);
                    if (0 != done.StatusCode)
                    {
                        throw new Exception($"Run failed with Status:{done.StatusCode}");
                    }

                }),
                client.Containers.GetContainerLogsAsync(container.ID, new ContainerLogsParameters
                {
                    Follow = true,
                    ShowStdout = true,
                    ShowStderr = true,
                }, cancellationToken, new Progress<string>(log => logger.LogDebug(new EventId(0, "output"), "{jobName} ->{log}", jobName, log))));
#pragma warning restore CS0618 // Type or member is obsolete

            }
            finally
            {
                try
                {
                    await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters());
                }
                catch(Exception ex)
                {
                    logger.LogCritical(ex, $"Failed to remove container id {container.ID} for image {launchConfig.image}");
                }
            }


        }


        public async Task StartContainerAsync(string jobName, string instanceParam, ILogger logger, CancellationToken cancellationToken = default(CancellationToken))
        {
            var launchConfig = readParameters<DockerStartParamsModel>(jobName);

            if (string.IsNullOrWhiteSpace(launchConfig.containerId))
                throw new Exception($"No containerId for launchConfig ");

            
            var client = createDockerClient();

            var started = DateTime.UtcNow;
            if (!await client.Containers.StartContainerAsync(launchConfig.containerId, new ContainerStartParameters { }, cancellationToken))
                throw new Exception("failed to start container");

            //The warning is incorrect
#pragma warning disable CS0618 // Type or member is obsolete
            await Task.WhenAll(Task.Run(async () =>
            {
                var done = await client.Containers.WaitContainerAsync(launchConfig.containerId, cancellationToken);
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
            }, cancellationToken, new Progress<string>(log =>logger.LogDebug(new EventId(0, "output"), "{jobName} ->{log}", jobName, log))));

#pragma warning restore CS0618 // Type or member is obsolete


        }
    }

    
}
