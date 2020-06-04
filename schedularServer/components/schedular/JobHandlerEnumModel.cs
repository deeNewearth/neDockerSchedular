using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace neSchedular.schedular
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum JobHandlerEnumModel { start, exec, run}
}
