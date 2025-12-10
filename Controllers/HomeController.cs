using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace webSitePro.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public HomeController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            try
            {
                var response = await CallSoapServiceManualAsync(username, password);

                if (response.Success)
                {
                    ViewBag.Success = $"Login successful! User data: {response.Data}";
                    ViewBag.Error = null;
                }
                else
                {
                    ViewBag.Error = response.ErrorMessage;
                    ViewBag.Success = null;
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error: {ex.Message}";
                ViewBag.Success = null;
            }

            return View("Index");
        }

        private async Task<SoapResponse> CallSoapServiceManualAsync(string username, string password)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // Создаем SOAP envelope вручную
                string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <Login xmlns=""urn:ICUTech.Intf-IICUTech"">
            <UserName>{EscapeXml(username)}</UserName>
            <Password>{EscapeXml(password)}</Password>
            <IPs></IPs>
        </Login>
    </soap:Body>
</soap:Envelope>";

                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "urn:ICUTech.Intf-IICUTech#Login");

                // Отправляем запрос
                var response = await client.PostAsync(
                    "http://isapi.mekashron.com/icu-tech/icutech-test.dll/soap/IICUTech",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    return new SoapResponse
                    {
                        Success = false,
                        ErrorMessage = $"HTTP Error: {response.StatusCode}"
                    };
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                // Извлекаем JSON из SOAP ответа
                var jsonResponse = ExtractJsonFromSoapResponse(responseContent);

                if (string.IsNullOrEmpty(jsonResponse))
                {
                    return new SoapResponse
                    {
                        Success = false,
                        ErrorMessage = "Invalid response format from server"
                    };
                }

                // Парсим JSON
                try
                {
                    using var doc = JsonDocument.Parse(jsonResponse);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("ResultCode", out var resultCode))
                    {
                        int code = resultCode.GetInt32();
                        if (code < 0)
                        {
                            string errorMsg = "Login failed";
                            if (root.TryGetProperty("ResultMessage", out var message))
                            {
                                errorMsg = message.GetString();
                            }
                            return new SoapResponse
                            {
                                Success = false,
                                ErrorMessage = errorMsg
                            };
                        }
                        else
                        {
                            // Успешный вход
                            return new SoapResponse
                            {
                                Success = true,
                                Data = FormatUserData(jsonResponse)
                            };
                        }
                    }
                    else
                    {
                        // Если нет ResultCode, но есть EntityId, считаем успешным
                        if (root.TryGetProperty("EntityId", out var entityId))
                        {
                            return new SoapResponse
                            {
                                Success = true,
                                Data = FormatUserData(jsonResponse)
                            };
                        }

                        return new SoapResponse
                        {
                            Success = false,
                            ErrorMessage = "Invalid response: No ResultCode found"
                        };
                    }
                }
                catch (JsonException)
                {
                    // Если не JSON, возвращаем как есть
                    return new SoapResponse
                    {
                        Success = true,
                        Data = responseContent
                    };
                }
            }
            catch (Exception ex)
            {
                return new SoapResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string ExtractJsonFromSoapResponse(string soapResponse)
        {
            try
            {
                // Ищем начало JSON
                int start = soapResponse.IndexOf("{");
                if (start == -1) return null;

                // Ищем конец JSON
                int end = soapResponse.LastIndexOf("}");
                if (end <= start) return null;

                return soapResponse.Substring(start, end - start + 1);
            }
            catch
            {
                return null;
            }
        }

        private string FormatUserData(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                var formatted = new StringBuilder();
                formatted.AppendLine("User Information:");
                formatted.AppendLine("-----------------");

                foreach (var property in root.EnumerateObject())
                {
                    formatted.AppendLine($"{property.Name}: {property.Value}");
                }

                return formatted.ToString();
            }
            catch
            {
                return jsonResponse;
            }
        }

        private string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }

    public class SoapResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Data { get; set; }
    }
}