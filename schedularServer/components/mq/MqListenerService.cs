using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using neMQConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace components.mq
{
    /// <summary>
    /// Defines what actions to take when for MQ messages
    /// </summary>
    public class ObserverActions
    {
        /// <summary>
        /// Whihc message we are subscribing to
        /// </summary>
        public MQObserverIdModel observer { get; set; }


    }
    

    public class MqListenerService : IHostedService
    {
        readonly IHackedAppLifeline _lifeline;
        readonly ILogger _logger;
        readonly IRabbitMQConnector _mq;
        readonly IConfiguration _config;
        readonly schedular.ISchedularService _schedularService;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var observers = _config.GetSection("mqObservers").Get<ObserverActions[]>();
                if (null == observers)
                    throw new Exception("config section mqObservers not found");

                await Task.WhenAll(observers.Select(o => _mq.AddObserverAsync(o.observer, e =>
                {
                    var k = e.message;


                    return Task.CompletedTask;
                })));

                
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to start MqListenerService");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning("MqListener shutting down. Taking down application");
            _lifeline.Shutdown();
            return Task.CompletedTask;

        }
    }
}
