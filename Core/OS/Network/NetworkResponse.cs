using System.Collections.Generic;
using System.Text;

namespace TheGame.Core.OS;

/// <summary>
/// Wrapper class for network responses that preserves HTTP status, headers, and body.
/// </summary>
public class NetworkResponse {
    /// <summary> The HTTP status code (e.g., 200, 404, 500). </summary>
    public int StatusCode { get; set; }
    
    /// <summary> Returns true if the status code indicates success (200-299). </summary>
    public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;
    
    /// <summary> The response headers. </summary>
    public Dictionary<string, string> Headers { get; set; } = new();
    
    /// <summary> The raw response body bytes. </summary>
    public byte[] BodyBytes { get; set; }
    
    /// <summary> The response body decoded as a UTF-8 string. </summary>
    public string BodyText => BodyBytes != null ? Encoding.UTF8.GetString(BodyBytes) : null;
    
    /// <summary> An error message if the request failed before receiving a response. </summary>
    public string ErrorMessage { get; set; }
}
