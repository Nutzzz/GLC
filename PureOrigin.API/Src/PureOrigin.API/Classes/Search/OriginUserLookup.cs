using System.Xml.Serialization;
using System.Collections.Generic;

namespace PureOrigin.API.Classes.Search
{
    [XmlRoot(ElementName = "users")]
    public class OriginUserLookup
    {
        [XmlElement(ElementName = "user")]
        public List<OriginUser> Users { get; set; }
    }
}
