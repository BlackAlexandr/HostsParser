using System.Collections.Generic;

namespace HostsParser.Models
{
    public class ParsedLine
    {
        public IEnumerable<Host> Hosts { get; }
        public string Type { get; }
        public Range Range { get; }

        public ParsedLine(IEnumerable<Host> hosts, string type, Range range)
        {
            Hosts = hosts;
            Type = type;
            Range = range;
        }
    }
}
