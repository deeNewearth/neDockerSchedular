using MassTransit;
using System;
using System.Collections.Generic;
using System.Text;

namespace neSchedular
{
    public class ExecuteJobTask: IConsumer
    {
        // the jobName as configured in JobSchedules
        public string jobName { get; set; }

        //instance data for the Job..
        public string JobParam { get; set; }

        public readonly static string Q_NAME = "docker-run";
    }
}
