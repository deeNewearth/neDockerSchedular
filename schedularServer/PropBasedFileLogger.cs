using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Display;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace schedularServer
{
    //copied most of this from https://github.com/serilog/serilog-extensions-logging-file/blob/dev/src/Serilog.Extensions.Logging.File/Microsoft/Extensions/Logging/FileLoggerExtensions.cs

    public static class PropBasedFileLogger
    {

        /// <summary>
        /// create sync files name with propery subfolder
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="loggingBuilder"></param>
        /// <param name="keyPropertyName">The log property used as folderName</param>
        /// <param name="defaultKey">to use if the property is not available</param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static ILoggingBuilder Serilog_withNamedPath(this ILoggingBuilder loggingBuilder,
            string keyPropertyName, string defaultKey,
            IConfiguration configuration)
        {
            
            if (loggingBuilder == null) throw new ArgumentNullException(nameof(loggingBuilder));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var config = configuration.Get<FileLoggingConfiguration>();
            if (string.IsNullOrWhiteSpace(config.LogFolder))
            {
                Console.WriteLine("Unable to add the file logger: no LogFolder was present in the configuration");
                return loggingBuilder;
            }

            var minimumLevel = GetMinimumLogLevel(configuration);
            var levelOverrides = GetLevelOverrides(configuration);

            if (config.LogFolder == null) throw new ArgumentNullException(nameof(config.LogFolder));

            if (string.IsNullOrWhiteSpace(config.FileFormat))
                config.FileFormat = @"log-{Date}.txt";

            if (config.OutputTemplate == null) throw new ArgumentNullException(nameof(config.OutputTemplate));

            var formatter = config.Json ?
                (ITextFormatter)new RenderedCompactJsonFormatter() :
                new MessageTemplateTextFormatter(config.OutputTemplate, null);

            //got the idea from https://github.com/serilog/serilog-sinks-rollingfile/issues/50
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(MicrosoftToSerilogLevel(minimumLevel))
                .Enrich.FromLogContext()

                .WriteTo.Async(w =>
                    w.Map(keyPropertyName, defaultKey, (prop, w) =>
                    {
                        var fileName = Environment.ExpandEnvironmentVariables(config.FileFormat);

                        w.RollingFile(
                            formatter,

                            System.IO.Path.Combine( config.logFileFolder(prop),fileName),

                            fileSizeLimitBytes: config.FileSizeLimitBytes,
                            retainedFileCountLimit: config.RetainedFileCountLimit,
                            shared: true,
                            flushToDiskInterval: TimeSpan.FromSeconds(2));
                    }
                
                ));

            if (!config.Json)
            {
                loggerConfig.Enrich.With<EventIdEnricher>();
            }

            foreach (var levelOverride in levelOverrides ?? new Dictionary<string, LogLevel>())
            {
                loggerConfig.MinimumLevel.Override(levelOverride.Key, MicrosoftToSerilogLevel(levelOverride.Value));
            }

            return loggingBuilder.AddSerilog(loggerConfig.CreateLogger(), dispose:true);
        }

        public static LogEventLevel MicrosoftToSerilogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                // as there is no match for 'None' in Serilog, pick the least logging possible
                case LogLevel.None:
                case LogLevel.Critical:
                    return LogEventLevel.Fatal;
                case LogLevel.Error:
                    return LogEventLevel.Error;
                case LogLevel.Warning:
                    return LogEventLevel.Warning;
                case LogLevel.Information:
                    return LogEventLevel.Information;
                case LogLevel.Debug:
                    return LogEventLevel.Debug;
                // ReSharper disable once RedundantCaseLabel
                case LogLevel.Trace:
                default:
                    return LogEventLevel.Verbose;
            }
        }

        private static LogLevel GetMinimumLogLevel(IConfiguration configuration)
        {
            var minimumLevel = LogLevel.Information;
            var defaultLevel = configuration["LogLevel:Default"];
            if (!string.IsNullOrWhiteSpace(defaultLevel))
            {
                if (!Enum.TryParse(defaultLevel, out minimumLevel))
                {
                    Console.WriteLine("The minimum level setting `{0}` is invalid", defaultLevel);
                    minimumLevel = LogLevel.Information;
                }
            }
            return minimumLevel;
        }

        private static Dictionary<string, LogLevel> GetLevelOverrides(IConfiguration configuration)
        {
            var levelOverrides = new Dictionary<string, LogLevel>();
            foreach (var overr in configuration.GetSection("LogLevel").GetChildren().Where(cfg => cfg.Key != "Default"))
            {
                if (!Enum.TryParse(overr.Value, out LogLevel value))
                {
                    Console.WriteLine("The level override setting `{0}` for `{1}` is invalid", overr.Value, overr.Key);
                    continue;
                }

                levelOverrides[overr.Key] = value;
            }

            return levelOverrides;
        }
    }

    class EventIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddOrUpdateProperty(new LogEventProperty("EventId", new ScalarValue(EventIdHash.Compute(logEvent.MessageTemplate.Text))));
        }
    }

    class FileLoggingConfiguration
    {
        //Since we will be ready the log files and sending them on the wire....
        //we want to keep the file sizes small 
        internal const long DefaultFileSizeLimitBytes = 1024 * 1024;
        internal const int DefaultRetainedFileCountLimit = 100;

        internal const string DefaultOutputTemplate =
            "{Timestamp:o} {RequestId,13} [{Level:u3}] {Message} ({EventId:x8}){NewLine}{Exception}";

        /// <summary>
        /// Filename to write. The filename may include <c>{Date}</c> to specify
        /// how the date portion of the filename is calculated. May include
        /// environment variables.
        /// </summary>
        public string FileFormat
        { get; set; }

        /// <summary>
        /// Folder where log files will be createf
        /// </summary>
        public string LogFolder
        { get; set; }

        /// <summary>
        /// If <c>true</c>, the log file will be written in JSON format.
        /// </summary>
        public bool Json
        { get; set; }

        /// <summary>
        /// The maximum size, in bytes, to which any single log file will be
        /// allowed to grow. For unrestricted growth, pass <c>null</c>. The
        /// default is 10 MB.
        /// </summary>
        public long? FileSizeLimitBytes
        { get; set; } = DefaultFileSizeLimitBytes;

        /// <summary>
        /// The maximum number of log files that will be retained, including
        /// the current log file. For unlimited retention, pass <c>null</c>.
        /// The default is 10.
        /// </summary>
        public int? RetainedFileCountLimit
        { get; set; } = DefaultRetainedFileCountLimit;

        /// <summary>
        /// The template used for formatting plain text log output.
        /// The default is "{Timestamp:o} {RequestId,13} [{Level:u3}] {Message} ({EventId:x8}){NewLine}{Exception}"
        /// </summary>
        public string OutputTemplate { get; set; } = DefaultOutputTemplate;

        public string logFileFolder(string prop)
        {
            return System.IO.Path.Combine(LogFolder, prop);
        }

       

    }
}
