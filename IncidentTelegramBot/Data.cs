using Newtonsoft.Json;
using System;

namespace IncidentTelegramBot
{
    internal class Data
    {
        public int? MessageId { get; set; }

        [JsonProperty("ID")]
        public int ID { get; set; }

        [JsonProperty("dataStart")]
        public DateTime DataStart { get; set; }

        [JsonProperty("dataEnd")]
        public DateTime? DataEnd { get; set; }

        [JsonProperty("zues")]
        public string Zues { get; set; }

        [JsonProperty("rues")]
        public string Rues { get; set; }

        [JsonProperty("uslugi_all")]
        public string UslugiAll { get; set; }

        [JsonProperty("IP")]
        public string IP { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("problem")]
        public string Problem { get; set; }
        public int CountSym => Problem.Length;
    }
}
