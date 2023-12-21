using System.Reflection;
using System.Text;
using MixinLib.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MixinLib.Internal
{
    public static partial class Utils
    {
        private class TypeDescJsonConverter : JsonConverter<TypeDescriptor>
        {
            public override TypeDescriptor ReadJson(JsonReader reader, Type objectType, TypeDescriptor existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, TypeDescriptor value, JsonSerializer serializer)
            {
                writer.WriteValue(value.ToString());
            }
        }

        private class TypeJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type typeToConvert) => typeof(Type).IsAssignableFrom(typeToConvert);

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                writer.WriteValue(value is Type val ? val.FullName : value);
            }
        }

        private class MethodBaseJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type typeToConvert) => typeof(MethodBase).IsAssignableFrom(typeToConvert);

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                writer.WriteValue(value?.ToString());
            }
        }

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(new()
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new SnakeCaseNamingStrategy(),
            },
            Formatting = Formatting.None,
            Converters = {
                new TypeDescJsonConverter(),
                new TypeJsonConverter(),
                new StringEnumConverter(),
                new MethodBaseJsonConverter()
            },
        });

        public static string DumpJson(object obj)
        {
            StringBuilder sb = new();
            StringWriter sw = new(sb);
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                Serializer.Serialize(writer, obj);
            }
            return sb.ToString();
        }
    }
}