using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace components.docker
{
    public class DockerExecParamsModel
    {
        /// <summary>
        /// The containerId to run
        /// </summary>
        public string containerId { get; set; }

        public string[] commands { get; set; }

    }
}
