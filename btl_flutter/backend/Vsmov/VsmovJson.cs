using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace backend.Vsmov;

/// <summary>
/// Cấu hình đọc JSON của vsmov. Nguồn trả kiểu không nhất quán (VD vote_average là "7.3",
/// episode_total lúc là chuỗi lúc là số) nên dùng converter "dễ dãi": sai kiểu → null thay vì lỗi cả response.
/// </summary>
public static class VsmovJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new LenientStringConverter(), new LenientIntConverter(), new LenientDoubleConverter() },
    };

    private static string RawText(ref Utf8JsonReader reader) =>
        Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan);

    private sealed class LenientStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String: return reader.GetString();
                case JsonTokenType.Number: return RawText(ref reader);
                case JsonTokenType.True: return "true";
                case JsonTokenType.False: return "false";
                default: reader.Skip(); return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private sealed class LenientIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.TryGetInt32(out var n) ? n
                        : reader.TryGetDouble(out var d) && d is >= int.MinValue and <= int.MaxValue ? (int)d : null;
                case JsonTokenType.String:
                    return int.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : null;
                default: reader.Skip(); return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue(); else writer.WriteNumberValue(value.Value);
        }
    }

    private sealed class LenientDoubleConverter : JsonConverter<double?>
    {
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    return reader.TryGetDouble(out var n) ? n : null;
                case JsonTokenType.String:
                    return double.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : null;
                default: reader.Skip(); return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue(); else writer.WriteNumberValue(value.Value);
        }
    }
}
