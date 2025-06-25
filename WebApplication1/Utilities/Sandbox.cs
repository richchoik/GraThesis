using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace WebApplication1.Utilities
{
    public static class Sandbox
    {
        public static string ProcessPayload(string jsonPayload)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Sandbox received payload: {jsonPayload}");
                JObject jsonObject = JObject.Parse(jsonPayload);

                // Luôn deserialize với TypeNameHandling.None để ngăn RCE
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None
                };
                object deserializedObject = JsonConvert.DeserializeObject<object>(jsonObject.ToString(), settings);
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Deserialized object with TypeNameHandling.None: {deserializedObject}");

                // Chuyển đổi thành JToken để dễ xử lý và loại bỏ $type
                JToken safeToken = JToken.FromObject(deserializedObject);

                // Loại bỏ tất cả thuộc tính $type trong JToken
                RemoveTypeProperties(safeToken);

                // Xử lý base64 nếu có
                if (safeToken is JObject safeObject)
                {
                    foreach (var property in safeObject.Properties())
                    {
                        if (property.Value.Type == JTokenType.String)
                        {
                            string value = property.Value.ToString();
                            if (IsBase64String(value) && value.Length >= 20)
                            {
                                try
                                {
                                    byte[] bytes = Convert.FromBase64String(value);
                                    string decoded = Encoding.UTF8.GetString(bytes);
                                    property.Value = decoded;
                                    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Successfully decoded base64 for {property.Name}: {decoded}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Failed to decode base64 for {property.Name}: {ex.Message}");
                                }
                            }
                        }
                    }
                    return safeObject.ToString(); // Trả về chuỗi JSON đã xử lý an toàn
                }

                return safeToken.ToString(); // Trả về chuỗi JSON an toàn nếu không phải JObject
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Sandbox error: {ex.Message}");
                return null;
            }
        }

        private static void RemoveTypeProperties(JToken token)
        {
            if (token is JObject obj)
            {
                obj.Remove("$type"); // Xóa $type nếu có
                foreach (var property in obj.Properties())
                {
                    RemoveTypeProperties(property.Value); // Đệ quy vào các phần tử con
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    RemoveTypeProperties(item); // Đệ quy vào mảng
                }
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