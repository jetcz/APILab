using APILab.Models;
using System.Text.Json.Serialization;

namespace APILab.Serialization;

[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(Todo))]
[JsonSerializable(typeof(TodoPage))]
[JsonSerializable(typeof(TodoRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}