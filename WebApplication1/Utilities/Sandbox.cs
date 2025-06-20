using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace WebApplication1.Utilities
{
    public static class Sandbox
    {
        public static object ProcessPayload(string jsonPayload)
    {
        try
        {
            JObject jsonObject = JObject.Parse(jsonPayload);
            bool containsBase64 = false;
            bool containsType = false;

            foreach (var property in jsonObject.Properties())
            {
                if (property.Value.Type == JTokenType.String)
                {
                    string value = property.Value.ToString();

                    //Chỉ giải mã nếu là base64 hợp lệ và có độ dài tối thiểu
                    if (IsBase64String(value) && value.Length >= 4)
                    {
                        containsBase64 = true;
                        try
                        {
                            byte[] bytes = Convert.FromBase64String(value);
                            string decoded = Encoding.UTF8.GetString(bytes);
                            property.Value = decoded;
                            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Decoded base64: {decoded}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Không thể giải mã base64: {value}, Lỗi: {ex.Message}");
                        }
                    }

                    if (value.Contains("$type"))
                    {
                        containsType = true;
                    }
                }
            }

            if (!containsBase64 && !containsType)
            {
                return jsonPayload; // Giữ nguyên nếu không có base64 hoặc $type
            }

            if (containsType)
            {
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None
                };
                return JsonConvert.DeserializeObject<object>(jsonObject.ToString(), settings);
            }

            return jsonObject.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Lỗi trong sandbox: {ex.Message}");
            return null;
        }
    }

        private static bool IsBase64String(string value)
        {
            // Kiểm tra null hoặc rỗng
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            // Độ dài phải chia hết cho 4 (yêu cầu cơ bản của base64)
            if (value.Length % 4 != 0)
            {
                return false;
            }

            // Chỉ cho phép ký tự base64 hợp lệ
            if (!Regex.IsMatch(value, @"^[a-zA-Z0-9\+/]+={0,2}$"))
            {
                return false;
            }

            // Yêu cầu chuỗi phải có ít nhất một ký tự đặc trưng của base64
            // Nếu không có, rất có thể đây không phải base64
            if (!value.Contains("+") && !value.Contains("/") && !value.EndsWith("="))
            {
                return false;
            }

            // Thử giải mã để xác nhận tính hợp lệ
            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}