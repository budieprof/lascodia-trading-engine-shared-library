using System.Diagnostics.CodeAnalysis;

namespace Lascodia.Trading.Engine.SharedApplication.Common.Models;

[ExcludeFromCodeCoverage]
public class ResponseData<T>
{
    public T? data { get; set; }
    public bool status { get; set; }
    public string? message { get; set; }
    public string? responseCode { get; set; }


    public static ResponseData<T> Init(T? data, bool status = false, string message = "", string responseCode = "")
    {
        return new ResponseData<T>(){
            data = data,
            status = status,
            message = message,
            responseCode = responseCode,
        };
    }
}
