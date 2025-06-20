using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json.Linq;
using WebApplication1.Utilities;

namespace WebApplication1.Middleware
{
    public class DeserializationSecurityModule : IHttpModule
    {
        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
        }

        private void OnBeginRequest(object sender, EventArgs e)
    {
        var application = (HttpApplication)sender;
        var request = application.Request;
        var response = application.Response;
        var context = application.Context;

        bool enableMiddleware = ConfigurationManager.AppSettings["EnableMiddleware"] == "true";
        bool enableSandbox = ConfigurationManager.AppSettings["EnableSandbox"] == "true";

        if (!enableMiddleware || request.HttpMethod != "POST" || !request.ContentType.Contains("application/json"))
        {
            return;
        }

        try
        {
            request.InputStream.Position = 0;
            string jsonPayload;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8, true, 1024, true))
            {
                jsonPayload = reader.ReadToEnd();
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Middleware read payload: {jsonPayload}");
            }

            if (IsPayloadSuspicious(jsonPayload))
            {
                response.StatusCode = 403;
                response.Write("Phát hiện payload đáng ngờ. Truy cập bị từ chối.");
                application.CompleteRequest();
                return;
            }

            string processedPayload = jsonPayload;

            if (enableSandbox)
            {
                processedPayload = Sandbox.ProcessPayload(jsonPayload)?.ToString();
                if (processedPayload == null)
                {
                    response.StatusCode = 400;
                    response.Write("Payload không hợp lệ");
                    application.CompleteRequest();
                    return;
                }
            }

            context.Items["ProcessedPayload"] = processedPayload;
        }
        catch (Exception ex)
        {
            if (ex is ThreadAbortException)
            {
                return; // Bỏ qua ThreadAbortException
            }
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Middleware error: {ex.Message}");
            response.StatusCode = 500;
            response.Write("Lỗi máy chủ nội bộ");
            application.CompleteRequest();
        }
    }
        private bool IsPayloadSuspicious(string jsonPayload)
        {
            try
            {
                JToken jsonToken = JToken.Parse(jsonPayload);
                return HasSuspiciousContent(jsonToken);
            }
            catch
            {
                return true; // JSON không hợp lệ thì chặn luôn
            }
        }

        private bool HasSuspiciousContent(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    // Phát hiện $type ở bất kỳ đâu
                    if (property.Name.Equals("$type", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Phát hiện $type: {property.Value}");
                        return true;
                    }
                    // Duyệt đệ quy vào giá trị của thuộc tính
                    if (HasSuspiciousContent(property.Value))
                    {
                        return true;
                    }
                }
            }
            else if (token is JArray arr)
            {
                foreach (var item in arr)
                {
                    if (HasSuspiciousContent(item))
                    {
                        return true;
                    }
                }
            }
            else if (token is JValue val && val.Type == JTokenType.String)
            {
                string value = val.ToString();
                // Kiểm tra thêm chuỗi base64 dài (tùy chọn)
                if (IsBase64String(value) && value.Length > 100)
                {
                    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Phát hiện base64 dài: {value.Length} ký tự");
                    return true;
                }
            }
            return false;
        }

        private bool IsBase64String(string input)
        {
            if (string.IsNullOrEmpty(input) || input.Length % 4 != 0) return false;
            try
            {
                Convert.FromBase64String(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose() { }
    }
}