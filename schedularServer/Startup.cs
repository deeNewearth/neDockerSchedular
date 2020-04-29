using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Slack;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

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
            services.AddTransient<components.docker.IDockerExecuter,components.docker.DockerExecuter>();

            services.AddAuthentication("Basic")
                    .AddScheme<components.authentication.VouchOptions, components.authentication.VouchAuthHandler>("Basic", null);
            

            //register all Jobs
            foreach (var t in components.schedular.ScheduledJob.mapHandlers)
            {
                services.AddTransient(t.Value);
            }

            services.AddSingleton<Quartz.Spi.IJobFactory, components.schedular.JobFactory>();
            services.AddSingleton<Quartz.ISchedulerFactory, Quartz.Impl.StdSchedulerFactory>();

            services.AddSingleton<components.schedular.ISchedularService, components.schedular.SchedularService>();
            services.AddHostedService<components.schedular.QuartzHostedService>();

            services.AddControllers();
        }

        static object SlackMessage(string channel, string username, string text, Exception ex, string source, string category)
        {
            var texts = new[] { text };
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

            app.UseExceptionHandler(
             builder =>
             {
                 builder.Run(
                   async context =>
                   {

                       var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                       //var error = reactBase.ErrorMessage.SetStatusGetResult(context, exception, loggerFactory.CreateLogger("Global-Exception"));
                       var error = new { message = "we cannot handle this " };
                       context.Response.ContentType = "application/json";

                       await context.Response.WriteAsync(JsonConvert.SerializeObject(error)).ConfigureAwait(false);
                   });
             });


            var slackKey = Configuration["notifications:slack"];
            Debug.Assert(!string.IsNullOrWhiteSpace(slackKey), "Put your slack key in global environment  notifications__slack for slack publish to work");

            if (!string.IsNullOrWhiteSpace(slackKey))
            {
                var webhookUrl = new Uri(slackKey);

                var criticalChannel = Configuration["notifications:criticalChannel"]?? "#criticalChannel";
                var normalChannel = Configuration["notifications:normalChannel"] ?? "#normalChannel";
                var botName = Configuration["notifications:botName"] ?? "revBot";
                var source = Configuration["notifications:sourceApplication"] ?? "neSchedular";

                //critical message
                loggerFactory.AddSlack(new SlackConfiguration
                {
                    webhookUrl = webhookUrl,
                    MinLevel = LogLevel.Critical,

                    slackFormatter = (text, cat, level, ex) => SlackMessage(criticalChannel, botName,$"*{text}*",ex, source,cat)
                }, env) ;

                var runDataName = typeof(components.schedular.ScheduledJob).FullName;
                //success done messages only
                loggerFactory.AddSlack(new SlackConfiguration
                {
                    webhookUrl = webhookUrl,

                    filter = (cat, level, ex) =>
                    {
                        if (runDataName == cat && 
                                level >= LogLevel.Information && level < LogLevel.Error)
                            return true;

                        return false;
                    },

                    slackFormatter = (text, cat, level, ex) => SlackMessage(normalChannel, botName, text, ex, source, cat)
                }, env); ; ;

            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            
        }
    }
}
