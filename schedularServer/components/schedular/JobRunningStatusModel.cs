using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace components.schedular
{
    /// <summary>
    /// Used to send runing status for a job
    /// </summary>
    public class JobRunningStatusModel
    {
        public JobInfoModel info { get; set; }

        public string[] logs { get; set; }

    }
}
