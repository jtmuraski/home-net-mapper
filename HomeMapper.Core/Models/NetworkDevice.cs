using System;
using System.Collections.Generic;
using System.Text;

namespace HomeMapper.Core.Models
{
    public class NetworkDevice
    {
        public int Id { get; set; }
        public string MacAddress { get; set; }
        public string IpAddress { get; set; }
        public string Vendor { get; set; }        // from OUI lookup
        public string? Hostname { get; set; }      // from DNS, may be null
        public string? FriendlyName { get; set; } // user-assigned later
        public bool IsKnown { get; set; }          // has user acknowledged it?
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
    }
}
