using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace schedularServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(configHost =>
                {
                    //we load job schedules from this extra file
                    //that was the container could export the file 
                    configHost.AddJsonFile("appSettings.jobSchedules.json", optional: false, reloadOnChange:true);
                })
                .ConfigureLogging((hostingContext, builder) =>
                {
                    builder.Serilog_withNamedPath(
                        "jobName","other",
                        hostingContext.Configuration.GetSection("FileLogger")
                        );
                    //builder.AddFile(hostingContext.Configuration.GetSection("FileLogger"));
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        
    }
}
