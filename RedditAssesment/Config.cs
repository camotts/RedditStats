using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RedditAssesment
{
    public class Config
    {
        public required string AccessToken;
        public required List<string> Subreddits;
        public required int DataCount;

        public static Config LoadConfig(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            {
                var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                return deserializer.Deserialize<Config>(reader);
            }
        }

    }

}
