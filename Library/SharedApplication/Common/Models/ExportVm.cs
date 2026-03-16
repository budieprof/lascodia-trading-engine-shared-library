using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Models;

[ExcludeFromCodeCoverage]
public class ExportVm
{
    public string FileName { get; set; }

    public string ContentType { get; set; }

    public byte[] Content { get; set; }
}
