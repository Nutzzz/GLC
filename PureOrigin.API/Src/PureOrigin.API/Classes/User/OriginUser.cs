using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using PureOrigin.API.Interfaces;
using PureOrigin.API.Extensions;

namespace PureOrigin.API
{
    [XmlRoot(ElementName = "user")]
    public class OriginUser : IOriginUser, ISerialisable
    {
        [XmlElement("EAID")]
        public string Username { get; set; }

        [XmlElement("userId")]
        public ulong UserId { get; set; }

        [XmlElement("personaId")]
        public ulong PersonaId { get; set; }

        public async Task<string> GetAvatarUrlAsync(AvatarSizeType sizeType = AvatarSizeType.LARGE)
        {
            var request = OriginAPI.CreateRequest(HttpMethod.Get, $"https://api1.origin.com/avatar/user/{UserId}/avatars?size={(int)sizeType}");

            var response = await OriginAPI.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var regex = Regex.Match(content, @"<link>(.*?)<\/link>");
                    if (regex.Success)
                    {
                        return regex.Value.Replace("<link>", "").Replace("</link>", "");
                    }
                }
            }
            return null;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings { ContractResolver = new CustomJsonResolver(), Formatting = Formatting.Indented });
        }
    }
}
