using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace components.schedular
{
    public class JobScheduleModel
    {
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

        public bool disabled { get; set; }

        /// <summary>
        /// JodData Map that is passed as Job Context
        /// </summary>
        public Dictionary<string,string> jobDataMap { get; set; }

        //jobDataMap defines
        public readonly static string TIMEOUT = @"timeout";

        public JobScheduleModel()
        {
            jobDataMap = new Dictionary<string, string>();
        }

    }

    /// <summary>
    /// section to read of configurations
    /// </summary>
    public class JobConfigSection
    {
        /// <summary>
        /// where we store config for Job spcific config
        /// </summary>
        static public readonly string FILELOGGERCONFIGSECTION = "FileLogger";
        public Dictionary<string, JobScheduleModel> jobs { get; set; }

        public JobConfigSection()
        {
            jobs = new Dictionary<string, JobScheduleModel>();
        }
    }
}
