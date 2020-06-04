using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neSchedular.filelogger;

namespace neSchedular
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    //we load job schedules from this extra file
                    //that was the container could export the file 
                    config.AddJsonFile("appSettings.jobSchedules.json", optional: false, reloadOnChange:true);

#if DEBUG
                    var customMode = Environment.GetEnvironmentVariable("DEV_APPMODE");
                    if (!string.IsNullOrWhiteSpace(customMode))
                    {
                        config.AddJsonFile($"appsettings.Development.{customMode}.json", optional: false, reloadOnChange: true);
                    }
#endif
                })
                .ConfigureLogging((hostingContext, builder) =>
                {
                    builder.Serilog_withNamedPath(
                        "jobName","other",
                        hostingContext.Configuration.GetSection(neSchedular.schedular.JobConfigSection.FILELOGGERCONFIGSECTION)
                        );
                    //builder.AddFile(hostingContext.Configuration.GetSection("FileLogger"));
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        
    }
}
