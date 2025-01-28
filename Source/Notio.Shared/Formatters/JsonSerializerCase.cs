namespace Notio.Shared.Formatters;

//
// Summary:
//     Enumerates the JSON serializer cases to use: None (keeps the same case), PascalCase,
//     or camelCase.
public enum JsonSerializerCase
{
    //
    // Summary:
    //     The none
    None,

    //
    // Summary:
    //     The pascal case (eg. PascalCase)
    PascalCase,

    //
    // Summary:
    //     The camel case (eg. camelCase)
    CamelCase
}