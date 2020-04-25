using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace components.schedular
{
    public enum JobHandlers { run, exec};

    public class JobScheduleModel
    {
        public static readonly string LAUNCH_PARAMS_CONFIG_KEY = @"LAUNCH_PARAMS";
        /// <summary>
        /// when to trigger this job
        /// use https://www.freeformatter.com/cron-expression-generator-quartz.html to make good ones
        /// </summary>
        public string cronStatement { get; set; }

        /// <summary>
        /// user readable description
        /// </summary>
        public string description { get; set; }


        public JobHandlerEnumModel? handler { get; set; }

        public static readonly IReadOnlyDictionary<JobHandlerEnumModel, Type> mapHandlers = new Dictionary<JobHandlerEnumModel, Type>
        {
            { JobHandlerEnumModel.run,typeof(DockerRunService)}
        };

    }

    /// <summary>
    /// section to read of configurations
    /// </summary>
    public class JobConfigSection
    {
        public Dictionary<string, JobScheduleModel> jobs { get; set; }

        public JobConfigSection()
        {
            jobs = new Dictionary<string, JobScheduleModel>();
        }
    }
}
