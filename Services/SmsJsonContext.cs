using System.Text.Json.Serialization;
using L850Manager.Models;

namespace L850Manager.Services;

[JsonSerializable(typeof(List<SmsMessage>))]
internal partial class SmsJsonContext : JsonSerializerContext;
