using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;

namespace Lascodia.Trading.Engine.SharedLibrary;

/// <summary>
/// Provides a set of helper methods for various operations.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Helper
{
    /// <summary>
    /// Determines whether the specified string is a valid integer number.
    /// </summary>
    /// <param name="val">The string to check.</param>
    /// <returns><c>true</c> if the string is a valid integer; otherwise, <c>false</c>.</returns>
    public static bool IsNumber(this string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return false;
        return int.TryParse(val.Trim(), out var _intVal);
    }

    /// <summary>
    /// Generates a scheduled ID based on the current UTC time and a random string.
    /// </summary>
    /// <returns>A string representing the scheduled ID.</returns>
    public static string GetScheduledId()
    {
        return DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + GetRandomString(3);
    }

    /// <summary>
    /// Checks if the specified enumerable is null or empty.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <param name="bindingList">The enumerable to check.</param>
    /// <returns><c>true</c> if the enumerable is null or empty; otherwise, <c>false</c>.</returns>
    public static bool IsEmptyOrNull<T>(this IEnumerable<T>? bindingList)
    {
        if (bindingList == null || !bindingList.Any())
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the name of the month corresponding to the specified value.
    /// </summary>
    /// <param name="value">The month value (1-12).</param>
    /// <returns>The name of the month, or the value as a string if out of range.</returns>
    public static string GetMonthName(int value)
    {
        var formatInfo = new DateTimeFormatInfo();
        if (value < 1 || value > 12) return value.ToString(CultureInfo.InvariantCulture);
        return formatInfo.GetMonthName(value);
    }

    /// <summary>
    /// Asynchronously retrieves the content of the specified URL.
    /// </summary>
    /// <param name="url">The URL to retrieve content from.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <returns>A task representing the asynchronous operation, with a string result containing the URL content.</returns>
    public static async Task<string> GetUrlContentAsync(string url, IHttpClientFactory httpClientFactory)
    {
        // Do not dispose HttpClient instances created by IHttpClientFactory
        // The factory manages the lifetime and connection pooling internally
        HttpClient client = httpClientFactory.CreateClient("ProxyClient");
        using (HttpResponseMessage response = await client.GetAsync(url))
        {
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            return content;
        }
    }

    /// <summary>
    /// Serializes the specified enumerable to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
    /// <param name="value">The enumerable to serialize.</param>
    /// <returns>A JSON string representation of the enumerable.</returns>
    public static string GetJson<T>(this IEnumerable<T> value)
    {
        return JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            });
    }

    /// <summary>
    /// Serializes the specified object to a JSON string.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <returns>A JSON string representation of the object.</returns>
    public static string GetJson(this object value)
    {
        return JsonConvert.SerializeObject(value,
            new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            });
    }

    /// <summary>
    /// Converts an enumeration to a list of NameId objects.
    /// </summary>
    /// <typeparam name="T">The type of the enumeration.</typeparam>
    /// <returns>A list of NameId objects representing the enumeration values.</returns>
    public static List<NameId> EnumToList<T>() where T : struct
    {
        Type enumType = typeof(T);
        if (enumType.BaseType == typeof(Enum))
        {
            Array enumValArray = Enum.GetValues(enumType);
            var enumValList = new List<NameId>(enumValArray.Length);
            enumValList.AddRange(from int val in enumValArray select new NameId { Name = GetEnum(enumType, val.ToString()).ToString(), Id = val, Description = GetEnumDescription((Enum)GetEnum(enumType, val.ToString())) });
            return enumValList;
        }
        return new List<NameId>();
    }

    /// <summary>
    /// Converts an enumeration to a list of its values.
    /// </summary>
    /// <typeparam name="T">The type of the enumeration.</typeparam>
    /// <returns>A list of enumeration values.</returns>
    public static List<T> EnumList<T>() where T : struct
    {
        Type enumType = typeof(T);
        if (enumType.BaseType == typeof(Enum))
        {
            Array enumValArray = Enum.GetValues(enumType);
            var enumValList = new List<T>(enumValArray.Length);
            enumValList.AddRange(from int val in enumValArray select (T)GetEnum(enumType, val.ToString()));
            return enumValList;
        }
        return new List<T>();
    }

    /// <summary>
    /// Parses a string to an enumeration value.
    /// </summary>
    /// <param name="enumType">The type of the enumeration.</param>
    /// <param name="val">The string representation of the enumeration value.</param>
    /// <returns>The parsed enumeration value.</returns>
    public static object GetEnum(Type enumType, string val)
    {
        return Enum.Parse(enumType, val);
    }

    /// <summary>
    /// Converts a list of objects to a CSV string.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to convert.</param>
    /// <returns>A CSV string representation of the list.</returns>
    public static string ListToCsv<T>(IEnumerable<T> list)
    {
        StringBuilder sList = new StringBuilder();

        Type type = typeof(T);
        var props = type.GetProperties();
        sList.Append(string.Join(",", props.Select(p => p.GetPropertyDescription())));
        sList.Append(Environment.NewLine);

        foreach (var element in list)
        {
            sList.Append(string.Join(",", props.Select(p => p.GetValue(element, null))));
            sList.Append(Environment.NewLine);
        }

        return sList.ToString();
    }

    /// <summary>
    /// Maps a dictionary to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="map">The dictionary containing property values.</param>
    /// <returns>An object of type T with properties set from the dictionary.</returns>
    public static T MapToObject<T>(this IDictionary<object, object> map)
    {
        Type type = typeof(T);
        var props = type.GetProperties();
        var names = props.Select(p => p.Name);
        T obj = (T)Activator.CreateInstance(type);
        foreach (var name in names)
        {
            PropertyInfo? prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (null != prop && prop.CanWrite && map.ContainsKey(name))
            {
                prop.SetValue(obj, map[name], null);
            }
        }
        return obj;
    }

    /// <summary>
    /// Generates an Excel sheet from a list of objects.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="value">The list of objects to export.</param>
    /// <param name="filePath">The file path to save the Excel sheet.</param>
    /// <param name="fileName">The name of the Excel sheet.</param>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A FileInfo object representing the generated Excel sheet.</returns>
    public static FileInfo GenerateExcelSheet<T>(this List<T> value, string filePath, string fileName, HttpContext context)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        FileInfo newFile = new FileInfo(fullPath);

        using (ExcelPackage xlPackage = new ExcelPackage(newFile))
        {
            ExcelWorksheet worksheet = xlPackage.Workbook.Worksheets.Add(fileName);
            if (worksheet != null)
            {
                int startRow = 1;
                int startColumn = 1;
                Type type = typeof(T);
                var props = type.GetProperties();
                var names = props.Select(p => p.Name);
                var columnCount = names.Count();
                int currentRow = startRow;
                int currentColumn = startColumn;
                // Set Header Rows
                foreach (var name in names)
                {
                    worksheet.Cells[startRow, currentColumn].Value = name;
                    currentColumn++;
                }
                using (ExcelRange r = worksheet.Cells[startRow, startColumn, startRow, (startColumn + columnCount - 1)])
                {
                    r.Style.Font.SetFromFont("Calibri", 12, true);
                    r.Style.Font.Color.SetColor(Color.Black);
                    r.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    r.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    r.Style.WrapText = true;
                }
                currentColumn = startColumn;
                // Set Table body rows
                currentRow++;
                foreach (var obj in value)
                {
                    foreach (var name in names)
                    {
                        PropertyInfo? prop = obj?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                        if (null != prop && prop.CanRead)
                        {
                            var val = prop.GetValue(obj);
                            worksheet.Cells[currentRow, currentColumn].Value = val;
                        }
                        currentColumn++;
                    }
                    currentColumn = startColumn;
                    currentRow++;
                }

                worksheet.HeaderFooter.OddHeader.CenteredText = fileName;
                worksheet.HeaderFooter.OddFooter.RightAlignedText =
                    $"Page {ExcelHeaderFooter.PageNumber} of {ExcelHeaderFooter.NumberOfPages}";
                worksheet.HeaderFooter.OddFooter.CenteredText = ExcelHeaderFooter.SheetName;
                worksheet.HeaderFooter.OddFooter.LeftAlignedText = ExcelHeaderFooter.FilePath + ExcelHeaderFooter.FileName;
            }
            xlPackage.Save();
        }
        return newFile;
    }

    /// <summary>
    /// Deserializes a JSON string to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="data">The JSON string to deserialize.</param>
    /// <returns>An object of type T.</returns>
    public static T StringToJson<T>(string data)
    {
        if (!string.IsNullOrWhiteSpace(data))
        {
            T? value = JsonConvert.DeserializeObject<T>(data);
            return value;
        }
        return default;
    }

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <param name="data">The JSON string to deserialize.</param>
    /// <returns>An object deserialized from the JSON string.</returns>
    public static object StringToJson(string data)
    {
        if (!string.IsNullOrWhiteSpace(data))
        {
            var value = JsonConvert.DeserializeObject(data);
            return value;
        }
        return default(Type);
    }

    /// <summary>
    /// Converts a byte array to a hexadecimal string.
    /// </summary>
    /// <param name="ba">The byte array to convert.</param>
    /// <returns>A hexadecimal string representation of the byte array.</returns>
    public static string ByteArrayToString(byte[] ba)
    {
        StringBuilder hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }

    /// <summary>
    /// Computes the SHA-512 hash of a string.
    /// </summary>
    /// <param name="value">The string to hash.</param>
    /// <returns>A hexadecimal string representation of the SHA-512 hash.</returns>
    public static string HashWithSha512(string value)
    {
        byte[] hash;
        var data = Encoding.UTF8.GetBytes(value);
        using (SHA512 shaM = SHA512.Create())
        {
            hash = shaM.ComputeHash(data);
        }
        return ByteArrayToString(hash);
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string.
    /// </summary>
    /// <param name="value">The string to hash.</param>
    /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
    public static string HashWithSha256(string value)
    {
        byte[] hash;
        var data = Encoding.UTF8.GetBytes(value);
        using (SHA256 shaM = SHA256.Create())
        {
            hash = shaM.ComputeHash(data);
        }
        return ByteArrayToString(hash);
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string and encodes it in Base64.
    /// </summary>
    /// <param name="rawData">The string to hash.</param>
    /// <returns>A Base64 string representation of the SHA-256 hash.</returns>
    public static string ComputeSha256HashWithBase64(string rawData)
    {
        string returnStr = "";
        using (SHA256 sha256Hash = SHA256.Create())
        {
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            returnStr = System.Convert.ToBase64String(bytes);
        }
        return returnStr;
    }

    /// <summary>
    /// Converts an object to a query string.
    /// </summary>
    /// <param name="obj">The object to convert.</param>
    /// <returns>A query string representation of the object.</returns>
    public static string GetQueryString(this object obj)
    {
        var properties = from p in obj.GetType().GetProperties()
                         where p.GetValue(obj, null) != null
                         select p.Name + "=" + HttpUtility.UrlEncode(p.GetValue(obj, null).ToString());

        return String.Join("&", properties.ToArray());
    }

    /// <summary>
    /// Converts a NameValueCollection to a query string.
    /// </summary>
    /// <param name="obj">The NameValueCollection to convert.</param>
    /// <returns>A query string representation of the NameValueCollection.</returns>
    public static string GetQueryString(this NameValueCollection obj)
    {
        if (obj == null) return string.Empty;
        StringBuilder queryString = new StringBuilder();
        foreach (var key in obj.AllKeys.Where(key => key != null))
        {
            if (queryString.Length > 0)
                queryString.Append("&");
            queryString.Append($"{HttpUtility.UrlEncode(key)}={HttpUtility.UrlEncode(obj[key])}");
        }
        return queryString.ToString();
    }

    /// <summary>
    /// Sends an HTTP POST request with form data asynchronously.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="requestParam">The form data to send.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResponseMessage result.</returns>
    /// <remarks>
    /// IMPORTANT: The caller is responsible for disposing the returned HttpResponseMessage to prevent memory leaks.
    /// Use a using statement or using declaration:
    /// <code>
    /// using (var response = await Helper.PostHttpFormAsync(...))
    /// {
    ///     // Process response
    /// }
    /// </code>
    /// </remarks>
    public static async Task<HttpResult> PostHttpFormAsync(string url, NameValueCollection requestParam, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        // Do not dispose HttpClient instances created by IHttpClientFactory
        HttpClient client = httpClientFactory.CreateClient("ProxyClient");
        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            headers.AllKeys.ToList().ForEach(s => request.Headers.Add(s!, headers[s]));
            var collection = new List<KeyValuePair<string, string>>();
            requestParam.AllKeys.ToList().ForEach(s => collection.Add(new(s!, requestParam.Get(s)!)));
            request.Content = new FormUrlEncodedContent(collection);
            using (var response = await client.SendAsync(request))
            {
                var content = await response.Content.ReadAsStringAsync();

                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                return new HttpResult
                {
                    StatusCode = (int)response.StatusCode,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    Content = content,
                    Headers = responseHeaders,
                    ReasonPhrase = response.ReasonPhrase
                };
            }
        }
    }

    /// <summary>
    /// Sends an HTTP POST request with JSON data asynchronously.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="requestParam">The JSON data to send.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResponseMessage result.</returns>
    /// <remarks>
    /// IMPORTANT: The caller is responsible for disposing the returned HttpResponseMessage to prevent memory leaks.
    /// Use a using statement or using declaration:
    /// <code>
    /// using (var response = await Helper.PostHttpJsonAsync(...))
    /// {
    ///     // Process response
    /// }
    /// </code>
    /// </remarks>
    public static async Task<HttpResult> PostHttpJsonAsync(string url, string requestParam, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config, string contentType = "application/json")
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        // Do not dispose HttpClient instances created by IHttpClientFactory
        HttpClient client = httpClientFactory.CreateClient("ProxyClient");
        using (var request = new HttpRequestMessage(HttpMethod.Post, url))
        {
            headers.AllKeys.ToList().ForEach(s => request.Headers.Add(s!, headers[s]));
            request.Content = new StringContent(requestParam, Encoding.UTF8, contentType);
            using (var response = await client.SendAsync(request))
            {
                var content = await response.Content.ReadAsStringAsync();

                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                return new HttpResult
                {
                    StatusCode = (int)response.StatusCode,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    Content = content,
                    Headers = responseHeaders,
                    ReasonPhrase = response.ReasonPhrase
                };
            }
        }
    }

    /// <summary>
    /// Sends an HTTP PUT request with JSON data asynchronously.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="requestParam">The JSON data to send.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResponseMessage result.</returns>
    /// <remarks>
    /// IMPORTANT: The caller is responsible for disposing the returned HttpResponseMessage to prevent memory leaks.
    /// Use a using statement or using declaration:
    /// <code>
    /// using (var response = await Helper.PutHttpJsonAsync(...))
    /// {
    ///     // Process response
    /// }
    /// </code>
    /// </remarks>
    public static async Task<HttpResult> PutHttpJsonAsync(string url, string requestParam, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config, string contentType = "application/json")
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        // Do not dispose HttpClient instances created by IHttpClientFactory
        HttpClient client = httpClientFactory.CreateClient("ProxyClient");
        using (var request = new HttpRequestMessage(HttpMethod.Put, url))
        {
            headers.AllKeys.ToList().ForEach(s => request.Headers.Add(s!, headers[s]));
            request.Content = new StringContent(requestParam, Encoding.UTF8, contentType);
            using (var response = await client.SendAsync(request))
            {
                var content = await response.Content.ReadAsStringAsync();

                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                return new HttpResult
                {
                    StatusCode = (int)response.StatusCode,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    Content = content,
                    Headers = responseHeaders,
                    ReasonPhrase = response.ReasonPhrase
                };
            }
        }
    }

    /// <summary>
    /// Sends an HTTP GET request with JSON data asynchronously.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResponseMessage result.</returns>
    /// <remarks>
    /// IMPORTANT: The caller is responsible for disposing the returned HttpResponseMessage to prevent memory leaks.
    /// Use a using statement or using declaration:
    /// <code>
    /// using (var response = await Helper.GetHttpJsonAsync(...))
    /// {
    ///     // Process response
    /// }
    /// </code>
    /// </remarks>
    public static async Task<HttpResult> GetHttpJsonAsync(string url, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config, string contentType = "application/json")
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        // Do not dispose HttpClient instances created by IHttpClientFactory
        HttpClient client = httpClientFactory.CreateClient("ProxyClient");
        using (var request = new HttpRequestMessage(HttpMethod.Get, url))
        {
            headers.AllKeys.ToList().ForEach(s => request.Headers.Add(s!, headers[s]));
            request.Content = new StringContent("", Encoding.UTF8, contentType);
            using (var response = await client.SendAsync(request))
            {
                var content = await response.Content.ReadAsStringAsync();

                var responseHeaders = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                return new HttpResult
                {
                    StatusCode = (int)response.StatusCode,
                    IsSuccessStatusCode = response.IsSuccessStatusCode,
                    Content = content,
                    Headers = responseHeaders,
                    ReasonPhrase = response.ReasonPhrase
                };
            }
        }
    }

    /// <summary>
    /// Represents the result of an HTTP request with automatic resource cleanup.
    /// </summary>
    public class HttpResult
    {
        /// <summary>
        /// Gets the HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; init; }

        /// <summary>
        /// Gets a value indicating whether the HTTP response was successful.
        /// </summary>
        public bool IsSuccessStatusCode { get; init; }

        /// <summary>
        /// Gets the content of the response as a string.
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// Gets the response headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; init; } = new();

        /// <summary>
        /// Gets the reason phrase which typically describes the status code.
        /// </summary>
        public string? ReasonPhrase { get; init; }
    }

    /// <summary>
    /// Sends an HTTP GET request and returns the result with automatic resource cleanup.
    /// This method is memory-safe as it handles HttpResponseMessage disposal internally.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResult containing the response data.</returns>
    public static async Task<HttpResult> GetHttpAsync(string url, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config, string contentType = "application/json")
    {
        var response = await GetHttpJsonAsync(url, headers, httpClientFactory, config, contentType);
        var content = response.Content;

        var responseHeaders = new Dictionary<string, string>();
        foreach (var header in response.Headers)
        {
            responseHeaders[header.Key] = string.Join(", ", header.Value);
        }

        return new HttpResult
        {
            StatusCode = (int)response.StatusCode,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            Content = content,
            Headers = responseHeaders,
            ReasonPhrase = response.ReasonPhrase
        };
    }

    /// <summary>
    /// Sends an HTTP POST request with JSON data and returns the result with automatic resource cleanup.
    /// This method is memory-safe as it handles HttpResponseMessage disposal internally.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="requestParam">The JSON data to send.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResult containing the response data.</returns>
    public static async Task<HttpResult> PostHttpJsonAsyncSafe(string url, string requestParam, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config, string contentType = "application/json")
    {
        var response = await PostHttpJsonAsync(url, requestParam, headers, httpClientFactory, config, contentType);
        var content = response.Content;

        var responseHeaders = new Dictionary<string, string>();
        foreach (var header in response.Headers)
        {
            responseHeaders[header.Key] = string.Join(", ", header.Value);
        }

        return new HttpResult
        {
            StatusCode = (int)response.StatusCode,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            Content = content,
            Headers = responseHeaders,
            ReasonPhrase = response.ReasonPhrase
        };
    }

    /// <summary>
    /// Sends an HTTP POST request with form data and returns the result with automatic resource cleanup.
    /// This method is memory-safe as it handles HttpResponseMessage disposal internally.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="requestParam">The form data to send.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResult containing the response data.</returns>
    public static async Task<HttpResult> PostHttpFormAsyncSafe(string url, NameValueCollection requestParam, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        var response = await PostHttpFormAsync(url, requestParam, headers, httpClientFactory, config);
        var content = response.Content;

        var responseHeaders = new Dictionary<string, string>();
        foreach (var header in response.Headers)
        {
            responseHeaders[header.Key] = string.Join(", ", header.Value);
        }

        return new HttpResult
        {
            StatusCode = (int)response.StatusCode,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            Content = content,
            Headers = responseHeaders,
            ReasonPhrase = response.ReasonPhrase
        };
    }

    /// <summary>
    /// Sends an HTTP PUT request with JSON data and returns the result with automatic resource cleanup.
    /// This method is memory-safe as it handles HttpResponseMessage disposal internally.
    /// </summary>
    /// <param name="url">The URL to send the request to.</param>
    /// <param name="requestParam">The JSON data to send.</param>
    /// <param name="headers">The headers to include in the request.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="config">The configuration containing proxy settings.</param>
    /// <param name="contentType">The content type of the request.</param>
    /// <returns>A task representing the asynchronous operation, with an HttpResult containing the response data.</returns>
    public static async Task<HttpResult> PutHttpJsonAsyncSafe(string url, string requestParam, NameValueCollection headers, IHttpClientFactory httpClientFactory, IConfiguration config, string contentType = "application/json")
    {
        var response = await PutHttpJsonAsync(url, requestParam, headers, httpClientFactory, config, contentType);
        var content = response.Content;

        var responseHeaders = new Dictionary<string, string>();
        foreach (var header in response.Headers)
        {
            responseHeaders[header.Key] = string.Join(", ", header.Value);
        }

        return new HttpResult
        {
            StatusCode = (int)response.StatusCode,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            Content = content,
            Headers = responseHeaders,
            ReasonPhrase = response.ReasonPhrase
        };
    }

    public static string GetUrlContent(string url)
    {
        var request = (HttpWebRequest)WebRequest.Create(url);
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var receiveStream = response.GetResponseStream())
        {
            if (receiveStream != null)
            {
                using (var readStream = new StreamReader(receiveStream))
                {
                    string content = readStream.ReadToEnd();
                    return content;
                }
            }
        }
        return "";
    }

    /// <summary>
    /// Gets the description attribute of an enumeration value.
    /// </summary>
    /// <param name="en">The enumeration value.</param>
    /// <returns>The description of the enumeration value.</returns>
    public static string GetEnumDescription(this Enum en)
    {
        Type type = en.GetType();
        MemberInfo[] memInfo = type.GetMember(en.ToString());
        if (memInfo.Length > 0)
        {
            object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attrs.Length > 0)
            {
                return ((DescriptionAttribute)attrs[0]).Description;
            }
        }
        return en.ToString();
    }

    /// <summary>
    /// Gets a custom attribute of an enumeration value.
    /// </summary>
    /// <typeparam name="T">The type of the attribute.</typeparam>
    /// <param name="en">The enumeration value.</param>
    /// <returns>The custom attribute of the enumeration value.</returns>
    public static T? GetEnumProperty<T>(this Enum en)
    {
        Type type = en.GetType();
        MemberInfo[] memInfo = type.GetMember(en.ToString());
        if (memInfo.Length > 0)
        {
            object[] attrs = memInfo[0].GetCustomAttributes(typeof(T), false);
            if (attrs.Length > 0)
            {
                return (T)attrs[0];
            }
        }
        return default(T);
    }

    /// <summary>
    /// Gets the value of a property from an object.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="src">The source object.</param>
    /// <param name="propName">The name of the property.</param>
    /// <returns>The value of the property.</returns>
    public static T? GetPropValue<T>(this object src, string propName)
    {
        return (T?)src.GetType()?.GetProperty(propName)?.GetValue(src, null);
    }

    /// <summary>
    /// Reformats a date string in MM/DD/YYYY format to a DateTime object.
    /// </summary>
    /// <param name="date">The date string to reformat.</param>
    /// <returns>A DateTime object representing the reformatted date.</returns>
    public static DateTime ReFormatDate(this string date)
    {
        var value = date.Split('/');
        var data = new DateTime(int.Parse(value[2]), int.Parse(value[0]), int.Parse(value[1]));
        return data;
    }

    /// <summary>
    /// Reformats a date and time string in MM/DD/YYYY HH:MM AM/PM format to a DateTime object.
    /// </summary>
    /// <param name="datetime">The date and time string to reformat.</param>
    /// <returns>A DateTime object representing the reformatted date and time.</returns>
    public static DateTime ReFormatDateFull(this string datetime)
    {
        var dates = datetime.Split(' ');
        var timePart = dates[1].Split(':');
        var hour = int.Parse(timePart[0]);
        var min = int.Parse(timePart[1]);
        var tt = dates[2];
        if (tt.ToLower() == "pm" && hour != 12)
        {
            hour = hour + 12;
        }
        if (tt.ToLower() == "am" && hour == 12)
        {
            hour = 0;
        }
        var value = dates[0].Split('/');
        var data = new DateTime(int.Parse(value[2]), int.Parse(value[0]), int.Parse(value[1]), hour, min, 0);
        return data;
    }

    /// <summary>
    /// Encodes a string to Base64.
    /// </summary>
    /// <param name="plainText">The string to encode.</param>
    /// <returns>A Base64 encoded string.</returns>
    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        return System.Convert.ToBase64String(plainTextBytes);
    }

    /// <summary>
    /// Decodes a Base64 encoded string.
    /// </summary>
    /// <param name="base64EncodedData">The Base64 encoded string to decode.</param>
    /// <returns>The decoded string.</returns>
    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
        return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
    }

    /// <summary>
    /// Validates whether a string is a valid email address.
    /// </summary>
    /// <param name="email">The email address to validate.</param>
    /// <returns><c>true</c> if the email address is valid; otherwise, <c>false</c>.</returns>
    public static bool IsEmailValid(this string email)
    {
        try
        {
            string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, pattern);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disables SSL certificate validation.
    /// </summary>
    public static void DisableCertificateValidation()
    {
        ServicePointManager.ServerCertificateValidationCallback =
            (s, certificate, chain, sslPolicyErrors) => true;
    }

    /// <summary>
    /// Removes all special characters from a string.
    /// </summary>
    /// <param name="value">The string to process.</param>
    /// <returns>A string with all special characters removed.</returns>
    public static string RemoveAllSpecialCharacters(this string value)
    {
        return Regex.Replace(value, "[^0-9a-zA-Z]+", "");
    }

    /// <summary>
    /// Reformats a date string in DD/MM/YYYY format to a DateTime object.
    /// </summary>
    /// <param name="date">The date string to reformat.</param>
    /// <returns>A DateTime object representing the reformatted date, or null if the format is invalid.</returns>
    public static DateTime? ReFormatDateDMY(this string date)
    {
        try
        {
            var value = date.Split('/');
            var data = new DateTime(int.Parse(value[2]), int.Parse(value[1]), int.Parse(value[0]));
            return data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Serializes an object to an XML string.
    /// </summary>
    /// <param name="obj">The object to serialize.</param>
    /// <returns>An XML string representation of the object.</returns>
    public static string ObjectToXml(this object obj)
    {
        XmlDocument xmlDoc = new XmlDocument();   //Represents an XML document, 
                                                  // Initializes a new instance of the XmlDocument class.          
        XmlSerializer xmlSerializer = new XmlSerializer(obj.GetType());
        // Creates a stream whose backing store is memory. 
        using (MemoryStream xmlStream = new MemoryStream())
        {
            xmlSerializer.Serialize(xmlStream, obj);
            xmlStream.Position = 0;
            //Loads the XML document from the specified string.
            xmlDoc.Load(xmlStream);
            return xmlDoc.InnerXml;
        }
    }

    /// <summary>
    /// Converts a string to a stream.
    /// <para><strong>⚠️ MEMORY LEAK WARNING: Caller MUST dispose the returned stream!</strong></para>
    /// </summary>
    /// <param name="this">The string to convert.</param>
    /// <returns>A MemoryStream containing the string data. <strong>MUST BE DISPOSED by caller to prevent memory leaks.</strong></returns>
    /// <remarks>
    /// <para><strong>⚠️ CRITICAL: The returned MemoryStream MUST be disposed by the caller to prevent memory leaks.</strong></para>
    /// <para>ALWAYS wrap the result in a using statement or call Dispose() explicitly when done.</para>
    /// <para></para>
    /// <para><strong>✓ CORRECT usage examples:</strong></para>
    /// <code>
    /// // Option 1: Using declaration (recommended)
    /// using var stream = myString.ToStream();
    /// // Use stream here...
    ///
    /// // Option 2: Using statement
    /// using (var stream = myString.ToStream())
    /// {
    ///     // Use stream here...
    /// }
    ///
    /// // Option 3: Manual disposal with try-finally
    /// var stream = myString.ToStream();
    /// try
    /// {
    ///     // Use stream here...
    /// }
    /// finally
    /// {
    ///     stream?.Dispose();
    /// }
    /// </code>
    /// <para><strong>✗ INCORRECT usage (WILL CAUSE MEMORY LEAK):</strong></para>
    /// <code>
    /// var stream = myString.ToStream(); // NO DISPOSAL - MEMORY LEAK!
    /// DoSomething(myString.ToStream()); // NO DISPOSAL - MEMORY LEAK!
    /// return myString.ToStream(); // Transfers disposal responsibility to caller
    /// </code>
    /// </remarks>
    public static Stream ToStream(this string @this)
    {
        var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, leaveOpen: true))
        {
            writer.Write(@this);
            writer.Flush();
        }
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Deserializes an XML string to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="this">The XML string to deserialize.</param>
    /// <returns>An object of type T.</returns>
    public static T? XmlToObject<T>(this string @this) where T : class
    {
        using (var stream = @this.Trim().ToStream())
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Document }))
        {
            return new XmlSerializer(typeof(T)).Deserialize(reader) as T;
        }
    }

    /// <summary>
    /// Converts a CSV string to a list of objects of type T.
    /// </summary>
    /// <typeparam name="T">The type of the objects.</typeparam>
    /// <param name="csv">The CSV string to convert.</param>
    /// <returns>A list of objects of type T.</returns>
    public static List<T> CsvToObject<T>(this string csv) where T : class
    {
        try
        {
            Type type = typeof(T);
            var props = type.GetProperties();
            var names = props.Select(p => p.Name);

            var headers = new List<string>();
            var result = new List<T>();
            int i = 0;
            foreach (string row in csv.Split('\n'))
            {
                if (!string.IsNullOrEmpty(row))
                {
                    int j = 0;
                    // Execute a loop over the columns.  
                    var dic = new Dictionary<string, string>();
                    foreach (string cell in row.Split(','))
                    {
                        if (i == 0)
                        {
                            headers.Add(cell.Trim());
                        }
                        else
                        {
                            if (headers.Count > j)
                            {
                                dic[headers[j]] = cell.Trim();
                            }
                        }
                        j++;
                    }

                    if (i > 0)
                    {
                        T obj = (T)Activator.CreateInstance(type);
                        foreach (var name in names)
                        {
                            PropertyInfo prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                            if (null != prop && prop.CanWrite && headers.Contains(name))
                            {
                                if (prop.PropertyType == typeof(double))
                                {
                                    prop.SetValue(obj, dic[name].ToNumber<double>(), null);
                                }
                                else if (prop.PropertyType == typeof(int))
                                {
                                    prop.SetValue(obj, dic[name].ToNumber<int>(), null);
                                }
                                else
                                {
                                    prop.SetValue(obj, dic[name], null);
                                }
                            }
                        }
                        result.Add(obj);
                    }
                    i++;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new List<T>();
        }
    }

    /// <summary>
    /// Converts a CSV string to a list of objects of a specified type.
    /// </summary>
    /// <param name="csv">The CSV string to convert.</param>
    /// <param name="type">The type of the objects.</param>
    /// <returns>A list of objects of the specified type.</returns>
    public static List<object> CsvToObject(this string csv, Type type)
    {
        try
        {
            var props = type.GetProperties();
            var names = props.Select(p => p.Name);

            var headers = new List<string>();
            var result = new List<object>();
            int i = 0;
            foreach (string row in csv.Split('\n'))
            {
                if (!string.IsNullOrEmpty(row))
                {
                    int j = 0;
                    // Execute a loop over the columns.  
                    var dic = new Dictionary<string, string>();
                    foreach (string cell in row.Split(','))
                    {
                        if (i == 0)
                        {
                            headers.Add(cell.Trim());
                        }
                        else
                        {
                            if (headers.Count > j)
                            {
                                dic[headers[j]] = cell.Trim();
                            }
                        }
                        j++;
                    }

                    if (i > 0)
                    {
                        object obj = Activator.CreateInstance(type);
                        foreach (var name in names)
                        {
                            PropertyInfo prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                            if (null != prop && prop.CanWrite && headers.Contains(name))
                            {
                                if (prop.PropertyType == typeof(double))
                                {
                                    prop.SetValue(obj, dic[name].ToNumber<double>(), null);
                                }
                                else if (prop.PropertyType == typeof(int))
                                {
                                    prop.SetValue(obj, dic[name].ToNumber<int>(), null);
                                }
                                else
                                {
                                    prop.SetValue(obj, dic[name], null);
                                }
                            }
                        }
                        result.Add(obj);
                    }
                    i++;
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new List<object>();
        }
    }

    /// <summary>
    /// Converts a string to a numeric value of type T.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="value">The string to convert.</param>
    /// <returns>The numeric value of type T.</returns>
    public static T ToNumber<T>(this string value) where T : IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T>
    {
        return ToNumberObject<T>(value);
    }

    private static dynamic ToNumberObject<T>(this string value) where T : IComparable, IFormattable, IConvertible, IComparable<T>, IEquatable<T>
    {
        T initial = default(T);
        if (typeof(T) == typeof(int))
        {
            return value.ToInt();
        }
        if (typeof(T) == typeof(long))
        {
            return value.ToLong();
        }
        if (typeof(T) == typeof(double))
        {
            return value.ToDouble();
        }
        if (typeof(T) == typeof(decimal))
        {
            return value.ToDecimal();
        }
        return initial;
    }

    /// <summary>
    /// Converts a string to an integer.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The integer value.</returns>
    public static int ToInt(this string value)
    {
        int output = 0;
        var val = int.TryParse(value, out output);
        return output;
    }

    /// <summary>
    /// Converts a string to a long integer.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The long integer value.</returns>
    public static long ToLong(this string value)
    {
        long output = 0;
        var val = long.TryParse(value, out output);
        return output;
    }

    /// <summary>
    /// Converts a string to a double.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The double value.</returns>
    public static double ToDouble(this string value)
    {
        double output = 0;
        var val = double.TryParse(value, out output);
        return output;
    }

    /// <summary>
    /// Converts a string to a decimal.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The decimal value.</returns>
    public static decimal ToDecimal(this string value)
    {
        decimal output = 0;
        var val = decimal.TryParse(value, out output);
        return output;
    }

    /// <summary>
    /// Decodes an HTML-encoded string.
    /// </summary>
    /// <param name="value">The HTML-encoded string to decode.</param>
    /// <returns>The decoded string.</returns>
    public static string HtmlDecode(this string value)
    {
        return HttpUtility.HtmlDecode(value);
    }

    /// <summary>
    /// Encodes a string to HTML.
    /// </summary>
    /// <param name="value">The string to encode.</param>
    /// <returns>The HTML-encoded string.</returns>
    public static string HtmlEncode(this string value)
    {
        return HttpUtility.HtmlEncode(value);
    }

    /// <summary>
    /// Generates a random string of the specified length.
    /// </summary>
    /// <param name="length">The length of the random string.</param>
    /// <returns>A random string.</returns>
    public static string GetRandomString(int length)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[length];
            rng.GetBytes(randomBytes);
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(randomBytes.Select(b => chars[b % chars.Length]).ToArray());
        }
    }

    /// <summary>
    /// Deserializes a JSON string to an object of type T.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>An object of type T.</returns>
    public static T JsonToObject<T>(this string json)
    {
        var model = JsonConvert.DeserializeObject<T>(json);
        return model;
    }

    /// <summary>
    /// Exports a list of objects to a CSV byte array.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="value">The list of objects to export.</param>
    /// <returns>A byte array representing the CSV data.</returns>
    public static byte[] ExportCsv<T>(List<T> value)
    {
        var str = ListToCsv(value);
        byte[] buffer = Encoding.ASCII.GetBytes(str);
        return buffer;
    }

    /// <summary>
    /// Exports a list of objects to an Excel byte array.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="value">The list of objects to export.</param>
    /// <returns>A byte array representing the Excel data.</returns>
    public static byte[] ExportTableToExcel<T>(this List<T> value)
    {
        byte[] result;
        using (ExcelPackage xlPackage = new ExcelPackage())
        {
            ExcelWorksheet worksheet = xlPackage.Workbook.Worksheets.Add("EXPORT");
            if (worksheet != null)
            {
                int startRow = 1;
                int startColumn = 1;
                Type type = typeof(T);
                var props = type.GetProperties();
                var names = props.Select(p => p.Name);
                var columnCount = names.Count();
                int currentRow = startRow;
                int currentColumn = startColumn;
                // Set Header Rows
                foreach (var prop in props)
                {
                    worksheet.Cells[startRow, currentColumn].Value = prop.GetPropertyDescription();
                    currentColumn++;
                }
                using (ExcelRange r = worksheet.Cells[startRow, startColumn, startRow, (startColumn + columnCount - 1)])
                {
                    r.Style.Font.SetFromFont("Calibri", 12, true);
                    r.Style.Font.Color.SetColor(Color.Black);
                    r.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    r.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    r.Style.WrapText = true;
                }
                currentColumn = startColumn;
                // Set Table body rows
                currentRow++;
                foreach (var obj in value)
                {
                    foreach (var name in names)
                    {
                        PropertyInfo prop = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                        if (null != prop && prop.CanRead)
                        {
                            var val = prop.GetValue(obj);
                            worksheet.Cells[currentRow, currentColumn].Value = val;
                        }
                        currentColumn++;
                    }
                    currentColumn = startColumn;
                    currentRow++;
                }

                worksheet.HeaderFooter.OddHeader.CenteredText = "EXPORT";
                worksheet.HeaderFooter.OddFooter.RightAlignedText =
                    $"Page {ExcelHeaderFooter.PageNumber} of {ExcelHeaderFooter.NumberOfPages}";
                worksheet.HeaderFooter.OddFooter.CenteredText = ExcelHeaderFooter.SheetName;
                worksheet.HeaderFooter.OddFooter.LeftAlignedText = ExcelHeaderFooter.FilePath + ExcelHeaderFooter.FileName;
            }
            result = xlPackage.GetAsByteArray();
        }
        return result;
    }

    /// <summary>
    /// Exports a list of objects to a PDF byte array.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="value">The list of objects to export.</param>
    /// <returns>A byte array representing the PDF data.</returns>
    public static byte[] ExportTableToPdf<T>(this List<T> value)
    {
        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            writer.SetCloseStream(false);
            using var pdfDoc  = new PdfDocument(writer);
            using var document = new Document(pdfDoc, iText.Kernel.Geom.PageSize.A4);
            document.SetMargins(20f, 15.5f, 20f, 15.5f);

            Type type = typeof(T);
            var props = type.GetProperties();

            var headerFont  = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var contentFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            var table = new Table(props.Length).UseAllAvailableWidth();

            // Header row
            foreach (var prop in props)
            {
                var cell = new Cell()
                    .Add(new Paragraph(prop.GetPropertyDescription().ToUpper())
                        .SetFont(headerFont)
                        .SetFontSize(7))
                    .SetPadding(5)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetVerticalAlignment(VerticalAlignment.BOTTOM);
                table.AddHeaderCell(cell);
            }

            // Data rows
            foreach (var obj in value)
            {
                foreach (var prop in props)
                {
                    PropertyInfo? pi = obj?.GetType().GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
                    string text = pi is { CanRead: true } ? $"{pi.GetValue(obj)}" : "";
                    table.AddCell(CreateTableCell(text, contentFont));
                }
            }

            document.Add(table);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Creates a PDF table cell with specified content and alignment.
    /// </summary>
    public static Cell CreateTableCell(
        string text,
        PdfFont? font = null,
        TextAlignment alignment = TextAlignment.CENTER)
    {
        var paragraph = new Paragraph(text).SetFontSize(8).SetTextAlignment(alignment);
        if (font is not null) paragraph.SetFont(font);

        return new Cell()
            .Add(paragraph)
            .SetPadding(2)
            .SetPaddingBottom(3)
            .SetVerticalAlignment(VerticalAlignment.BOTTOM)
            .SetTextAlignment(alignment);
    }

    /// <summary>
    /// Generates a unique password of the specified length.
    /// </summary>
    /// <param name="length">The length of the password.</param>
    /// <returns>A unique password.</returns>
    public static string GetUniquePassword(int length)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] randomBytes = new byte[length];
            rng.GetBytes(randomBytes);
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            return new string(randomBytes.Select(b => valid[b % valid.Length]).ToArray());
        }
    }

    /// <summary>
    /// Formats an XML string with indentation.
    /// </summary>
    /// <param name="xml">The XML string to format.</param>
    /// <returns>The formatted XML string.</returns>
    public static string PrintXML(string xml)
    {
        string result = "";

        try
        {
            using (MemoryStream mStream = new MemoryStream())
            {
                using (XmlTextWriter writer = new XmlTextWriter(mStream, Encoding.Unicode))
                {
                    XmlDocument document = new XmlDocument();
                    // Load the XmlDocument with the XML.
                    document.LoadXml(xml);

                    writer.Formatting = System.Xml.Formatting.Indented;

                    // Write the XML into a formatting XmlTextWriter
                    document.WriteContentTo(writer);
                    writer.Flush();
                    mStream.Flush();

                    // Have to rewind the MemoryStream in order to read
                    // its contents.
                    mStream.Position = 0;

                    // Read MemoryStream contents into a StreamReader.
                    using (StreamReader sReader = new StreamReader(mStream))
                    {
                        // Extract the text from the StreamReader.
                        string formattedXml = sReader.ReadToEnd();
                        result = formattedXml;
                    }
                }
            }
        }
        catch (XmlException)
        {
            result = xml;
        }
        return result;
    }

    /// <summary>
    /// Converts a hexadecimal string to a byte array.
    /// </summary>
    /// <param name="hex">The hexadecimal string to convert.</param>
    /// <returns>A byte array representing the hexadecimal string.</returns>
    public static byte[] StringToByteArray(String hex)
    {
        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        }
        return bytes;
    }

    /// <summary>
    /// Converts a UTC DateTime to a local DateTime with a specified timezone offset.
    /// </summary>
    /// <param name="dt">The UTC DateTime to convert.</param>
    /// <param name="timezoneOffset">The timezone offset in minutes.</param>
    /// <returns>The local DateTime.</returns>
    public static DateTime FromUTC(this DateTime dt, int timezoneOffset)
    {
        dt = dt.AddMinutes(-1 * timezoneOffset);
        return dt;
    }

    /// <summary>
    /// Converts a local DateTime to a UTC DateTime with a specified timezone offset.
    /// </summary>
    /// <param name="dt">The local DateTime to convert.</param>
    /// <param name="timezoneOffset">The timezone offset in minutes.</param>
    /// <returns>The UTC DateTime.</returns>
    public static DateTime ToUTC(this DateTime dt, int timezoneOffset)
    {
        dt = dt.AddMinutes(timezoneOffset);
        return dt;
    }

    /// <summary>
    /// Converts a UTC DateTime to Nigeria local time.
    /// </summary>
    /// <param name="dt">The UTC DateTime to convert.</param>
    /// <returns>The Nigeria local DateTime.</returns>
    public static DateTime FromUTCNigeria(this DateTime dt)
    {
        return FromUTC(dt, -60);
    }

    /// <summary>
    /// Converts a Nigeria local DateTime to UTC.
    /// </summary>
    /// <param name="dt">The Nigeria local DateTime to convert.</param>
    /// <param name="timezoneOffset">The timezone offset in minutes.</param>
    /// <returns>The UTC DateTime.</returns>
    public static DateTime ToUTCNigeria(this DateTime dt, int timezoneOffset)
    {
        return ToUTC(dt, -60);
    }

    /// <summary>
    /// Gets the description attribute of a property.
    /// </summary>
    /// <param name="prop">The property to get the description for.</param>
    /// <returns>The description of the property.</returns>
    public static string GetPropertyDescription(this PropertyInfo prop)
    {
        object[] attrs = prop.GetCustomAttributes(typeof(DescriptionAttribute), false);
        if (attrs.Length > 0)
        {
            return ((DescriptionAttribute)attrs[0]).Description;
        }

        return prop.Name;
    }

    /// <summary>
    /// Gets the value of a claim from a collection of claims.
    /// </summary>
    /// <param name="claims">The collection of claims.</param>
    /// <param name="type">The type of the claim to retrieve.</param>
    /// <returns>The value of the claim, or null if not found.</returns>
    public static string? GetClaimValue(this IEnumerable<System.Security.Claims.Claim> claims, string type)
    {
        return claims.FirstOrDefault(s => s.Type == type)?.Value;
    }

    /// <summary>
    /// Gets an enumerable of types that are subclasses of a specified type.
    /// </summary>
    /// <typeparam name="T">The base type.</typeparam>
    /// <param name="constructorArgs">The constructor arguments for the types.</param>
    /// <returns>An enumerable of types that are subclasses of the specified type.</returns>
    public static IEnumerable<T> GetEnumerableOfType<T>(params object[] constructorArgs) where T : class, IComparable<T>
    {
        List<T> objects = new List<T>();
        foreach (Type type in
            Assembly.GetAssembly(typeof(T))?.GetTypes()
            .Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))))
        {
            objects.Add((T)Activator.CreateInstance(type, constructorArgs));
        }
        objects.Sort();
        return objects;
    }

    /// <summary>
    /// Gets a basic authorization header value.
    /// </summary>
    /// <param name="username">The username for the authorization.</param>
    /// <param name="password">The password for the authorization.</param>
    /// <returns>A basic authorization header value.</returns>
    public static string GetBasicAuthorizationHeader(string? username, string? password)
    {
        var base64string = Base64Encode($"{username}:{password}");
        return $"Basic {base64string}";
    }

    /// <summary>
    /// Gets the expiration time of a JWT token.
    /// </summary>
    /// <param name="token">The JWT token.</param>
    /// <returns>The expiration time of the token as a Unix timestamp.</returns>
    public static long GetTokenExpirationTime(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(token);
        var tokenExp = jwtSecurityToken.Claims.First(claim => claim.Type.Equals("exp")).Value;
        var ticks = long.Parse(tokenExp);
        return ticks;
    }

    /// <summary>
    /// Checks whether a JWT token is valid based on its expiration time.
    /// </summary>
    /// <param name="token">The JWT token to check.</param>
    /// <returns><c>true</c> if the token is valid; otherwise, <c>false</c>.</returns>
    public static bool CheckTokenIsValid(string token)
    {
        var tokenTicks = GetTokenExpirationTime(token);
        var tokenDate = DateTimeOffset.FromUnixTimeSeconds(tokenTicks).UtcDateTime;

        var now = DateTime.Now.ToUniversalTime();

        var valid = tokenDate >= now;

        return valid;
    }

    /// <summary>
    /// Converts a string to camel case.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>The camel case representation of the string.</returns>
    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    /// <summary>
    /// Encrypts a string using AES encryption.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <param name="key">The encryption key.</param>
    /// <param name="iv">The initialization vector.</param>
    /// <returns>The encrypted string.</returns>
    public static string Encrypt(string plainText, string key, string iv)
    {
        return Encrypt(plainText, Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(iv));
    }

    /// <summary>
    /// Decrypts a string using AES encryption.
    /// </summary>
    /// <param name="cipherText">The cipher text to decrypt.</param>
    /// <param name="key">The decryption key.</param>
    /// <param name="iv">The initialization vector.</param>
    /// <returns>The decrypted string.</returns>
    public static string Decrypt(string cipherText, string key, string iv)
    {
        return Decrypt(cipherText, Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(iv));
    }

    /// <summary>
    /// Encrypts a string using AES encryption with byte arrays.
    /// </summary>
    /// <param name="plainText">The plain text to encrypt.</param>
    /// <param name="key">The encryption key as a byte array.</param>
    /// <param name="iv">The initialization vector as a byte array.</param>
    /// <returns>The encrypted string.</returns>
    public static string Encrypt(string plainText, byte[] key, byte[] iv)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;
            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                }
                return Convert.ToBase64String(msEncrypt.ToArray());
            }
        }
    }

    /// <summary>
    /// Decrypts a string using AES encryption with byte arrays.
    /// </summary>
    /// <param name="cipherText">The cipher text to decrypt.</param>
    /// <param name="key">The decryption key as a byte array.</param>
    /// <param name="iv">The initialization vector as a byte array.</param>
    /// <returns>The decrypted string.</returns>
    public static string Decrypt(string cipherText, byte[] key, byte[] iv)
    {
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = key;
            aesAlg.IV = iv;
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
            using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Computes a 16-byte hash of a string using SHA-256.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>A 16-byte hash of the string.</returns>
    public static byte[] To16BytesHash(this string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(byteArray);
            byte[] truncatedHash = new byte[16];
            Array.Copy(hash, truncatedHash, 16);
            return truncatedHash;
        }
    }

    /// <summary>
    /// Converts a string to a fixed-length 16-byte array.
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>A 16-byte array representing the string.</returns>
    public static byte[] To16BytesFixedLength(this string input)
    {
        if (input.Length > 16)
        {
            input = input.Substring(0, 16);
        }
        else if (input.Length < 16)
        {
            input = input.PadRight(16, '\0');
        }

        return Encoding.UTF8.GetBytes(input);
    }

    /// <summary>
    /// Converts a string to a 16-byte UTF-8 encoded array.
    /// </summary>
    /// <param name="input">The string to convert.</param>
    /// <returns>A 16-byte UTF-8 encoded array representing the string.</returns>
    public static byte[] To16BytesUTF8(this string input)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(input);

        if (byteArray.Length > 16)
        {
            Array.Resize(ref byteArray, 16);
        }
        else if (byteArray.Length < 16)
        {
            Array.Resize(ref byteArray, 16);
        }

        return byteArray;
    }

    /// <summary>
    /// Represents a name and ID pair with an optional description.
    /// </summary>
    public class NameId
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the ID.
        /// </summary>
        public dynamic? Id { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }
    }
}
