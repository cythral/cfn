using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using RichardSzalay.MockHttp;

namespace Tests
{
    public static class Extensions
    {
        public static void WithJsonPayload<T>(this MockedRequest handler, T payload)
        {
            handler.WithContent(Serialize(payload));
        }

        private static string Serialize<T>(T toSerialize)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Serialize<T>(toSerialize, options);
        }
    }
}