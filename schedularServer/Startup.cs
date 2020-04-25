using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Slack;
using Microsoft.Extensions.Options;

namespace schedularServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddTransient<components.schedular.DockerRunService>();

            services.AddSingleton<Quartz.Spi.IJobFactory, components.schedular.JobFactory>();
            services.AddSingleton<Quartz.ISchedulerFactory, Quartz.Impl.StdSchedulerFactory>();

            services.AddHostedService<components.schedular.QuartzHostedService>();

            services.AddControllers();
        }

        static object SlackMessage(string channel, string username, string text, Exception ex, string source, string category)
        {
            var texts = new[] { $"*{text}*" };
            if (null != ex)
                texts = texts.Concat(new[] { $"Exception: ->`{ex}`" }).ToArray();

            var blocks = texts.Select(text =>(object) new
            {
                type = "section",
                text = new{type = "mrkdwn", text }
            }).Concat(new[] { new {
                type = "section",

                fields = new[]{ $"*Category:*\n{category}", $"*Source:*\n{source}"

                }.Select(text => new{type = "mrkdwn", text })
            } }).ToArray();

            return new { channel, username, blocks };

        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var slackKey = Configuration["notifications:slack"];
            Debug.Assert(!string.IsNullOrWhiteSpace(slackKey), "Put your slack key in global environment  notifications__slack for slack publish to work");

            if (!string.IsNullOrWhiteSpace(slackKey))
            {
                var webhookUrl = new Uri($"https://hooks.slack.com/services/{slackKey}");

                var criticalChannel = Configuration["notifications:criticalChannel"]?? "#criticalChannel";
                var normalChannel = Configuration["notifications:normalChannel"] ?? "#normalChannel";
                var botName = Configuration["notifications:botName"] ?? "revBot";
                var source = Configuration["notifications:sourceApplication"] ?? "neSchedular";

                //critical message
                loggerFactory.AddSlack(new SlackConfiguration
                {
                    webhookUrl = webhookUrl,
                    MinLevel = LogLevel.Error,

                    slackFormatter = (text, cat, level, ex) => SlackMessage(criticalChannel, botName,text,ex, source,cat)
                }, env) ;

                var g = typeof(components.schedular.RunData);
                var runDataName = typeof(components.schedular.RunData).FullName;
                //task done
                loggerFactory.AddSlack(new SlackConfiguration
                {
                    webhookUrl = webhookUrl,

                    filter = (cat, level, ex) =>
                    {
                        if (level >= LogLevel.Information && runDataName == cat)
                            return true;

                        return false;
                    },

                    slackFormatter = (text, cat, level, ex) => SlackMessage(normalChannel, botName, text, ex, source, cat)
                }, env); ; ;

            }



            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
