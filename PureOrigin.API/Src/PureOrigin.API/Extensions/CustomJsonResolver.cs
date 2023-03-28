using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PureOrigin.API.Extensions
{
    public class CustomJsonResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            return objectType.GetProperties().Where(pi => !Attribute.IsDefined(pi, typeof(JsonIgnoreSerialisationAttribute))).ToList<MemberInfo>();
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> list = base.CreateProperties(type, memberSerialization);
            foreach (JsonProperty prop in list)
            {
                prop.PropertyName = prop.UnderlyingName;
            }
            return list;
        }
    }
}
