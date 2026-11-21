using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CorePDF.Contents;

namespace CorePDF.Editor.API
{
    /// <summary>
    /// JSON settings for reading and writing editor documents.
    /// </summary>
    public static class DocumentJson
    {
        // Only these types can be created from the "$type" value in a document, so the
        // request can't choose arbitrary .NET types. The discriminators match the names
        // Newtonsoft's TypeNameHandling wrote, so documents saved by older versions still load.
        private static readonly Type[] ContentTypes =
        {
            typeof(TextBox),
            typeof(Barcode),
            typeof(Shape),
            typeof(Image),
            typeof(Table)
        };

        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            // older documents put "$type" on every object, not only first or only on content
            AllowOutOfOrderMetadataProperties = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { AddContentTypes }
            }
        };

        private static void AddContentTypes(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Type != typeof(Content)) return;

            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
            };

            foreach (var type in ContentTypes)
            {
                typeInfo.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, $"{type.FullName}, CorePDF"));
            }
        }
    }
}
