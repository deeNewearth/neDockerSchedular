using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace neSchedular.docker
{
    public class DockerExecParamsModel
    {
        /// <summary>
        /// The containerId to run
        /// </summary>
        public string containerId { get; set; }

        public string[] commands { get; set; }

        /// <summary>
        /// docker exec eats exit code.. so we mostly run command using a Bash script whihc echos "command_failed" when comsthing fails.
        /// If this is set to true we simply pass the command
        /// </summary>
        public bool runNakedCommand { get; set; }

    }
}
