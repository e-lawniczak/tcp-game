using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpGameSharedUtility
{
    public enum Command
    {
        bye,
        message,
        input

    }

    public class Packet
    {
        [JsonProperty("command")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Command Command { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }

        public Packet(Command command, string message = "")
        {
            Command = command;
            Message = message;
        }

        public override string ToString()
        {
            return string.Format(
                "[Packet:\n" +
                "  Command=`{0}`\n" +
                "  Message=`{1}`]",
                Command, Message);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, new StringEnumConverter());
        }

        public static Packet FromJson(string jsonData)
        {
            return JsonConvert.DeserializeObject<Packet>(jsonData);

        }
    }
}
