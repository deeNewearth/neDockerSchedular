using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace neSchedular.docker
{
    public class RunParamsModel
    {
        public string image { get; set; }

        public string[] commands { get; set; }

        public string[] entryPoint { get; set; }

        public string[] env { get; set; }
    }
        
}
