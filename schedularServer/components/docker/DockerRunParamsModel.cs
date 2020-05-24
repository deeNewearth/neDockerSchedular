using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace components.docker
{
    public class DockerRunParamsModel
    {
        public string image { get; set; }

        public string[] commands { get; set; }

        public string[] entryPoint { get; set; }

        public string[] env { get; set; }
    }
}
