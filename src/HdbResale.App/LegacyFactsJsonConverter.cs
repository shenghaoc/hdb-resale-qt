using System.Text.Json;
using System.Text.Json.Serialization;
using HdbResale.Domain;
namespace HdbResale.App;

// Historical M5 reports have an explicitly frozen six-field facts contract.
// Register only for that report. Full ImportResult serialization stays v2.
internal sealed class LegacyFactsJsonConverter : JsonConverter<TransactionFacts>
{
    public override TransactionFacts Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("The legacy report converter is write-only.");
    public override void Write(Utf8JsonWriter writer, TransactionFacts value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("Month"); JsonSerializer.Serialize(writer, value.Month, options);
        writer.WriteString("Town", value.Town); writer.WriteString("Block", value.Block);
        writer.WriteString("Street", value.Street); writer.WriteString("FlatType", value.FlatType);
        writer.WriteNumber("Price", value.Price);
        writer.WriteEndObject();
    }
}
