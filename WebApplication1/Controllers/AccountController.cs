using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Web.Mvc;
using WebApplication1.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WebApplication1.Controllers
{
    public class AccountController : Controller
    {
        // Danh sách tạm để thử nghiệm
        private static List<User> users = new List<User>
        {
            new User { Id = 1, Username = "admin", Password = "admin123" }
        };

        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        public class LoginModel
        {
            public dynamic Username { get; set; }
            public dynamic Password { get; set; }
        }

        [HttpPost]
        public ActionResult Login(string unused = null)
        {
            try
            {
                // Lấy payload từ middleware/sandbox hoặc request gốc
                string processedPayload = HttpContext.Items["ProcessedPayload"] as string;
                string jsonPayload;

                if (!string.IsNullOrEmpty(processedPayload))
                {
                    jsonPayload = processedPayload;
                }
                else
                {
                    Request.InputStream.Position = 0;
                    using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                    {
                        jsonPayload = reader.ReadToEnd();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Raw JSON: {jsonPayload}");

                if (string.IsNullOrEmpty(jsonPayload))
                {
                    return Json(new { success = false, message = "No data received" });
                }

                // Parse JSON để kiểm tra
                JObject jsonObject = JObject.Parse(jsonPayload);
                JToken usernameToken = jsonObject["Username"] ?? jsonObject["username"];
                JToken passwordToken = jsonObject["Password"] ?? jsonObject["password"];

                string username = null;
                string password = null;

                if (usernameToken != null && usernameToken.Type == JTokenType.Object && usernameToken["$type"] != null)
                {
                    // Deserialize với TypeNameHandling.All để cho phép RCE
                    object dangerousObject = JsonConvert.DeserializeObject<object>(usernameToken.ToString(), new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    });
                    System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Deserialized dangerous object: {dangerousObject}");
                }
                else
                {
                    username = usernameToken?.ToString();
                }

                password = passwordToken?.ToString();

                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Username: {username}, Password: {password}");

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "Invalid input" });
                }

                var user = users.FirstOrDefault(u => u.Username == username && u.Password == password);
                if (user != null)
                {
                    Session["UserId"] = user.Id;
                    Session["Username"] = user.Username;
                    return Json(new { success = true, message = "Login successful" });
                }
                return Json(new { success = false, message = "Invalid credentials" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Login Error: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpGet]
        public ActionResult Signup()
        {
            return View();
        }

        public class SignupModel
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [HttpPost]
        public ActionResult Signup(string unused = null)
        {
            try
            {
                string processedPayload = HttpContext.Items["ProcessedPayload"] as string;
                string jsonPayload;

                if (!string.IsNullOrEmpty(processedPayload))
                {
                    jsonPayload = processedPayload;
                }
                else
                {
                    Request.InputStream.Position = 0;
                    using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                    {
                        jsonPayload = reader.ReadToEnd();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Raw JSON received: {jsonPayload}");

                if (string.IsNullOrEmpty(jsonPayload))
                {
                    return Json(new { success = false, message = "No data received" });
                }

                JObject jsonObject = JObject.Parse(jsonPayload);
                JToken usernameToken = jsonObject["Username"] ?? jsonObject["username"];
                JToken passwordToken = jsonObject["Password"] ?? jsonObject["password"];

                string username = null;
                string password = null;

                if (usernameToken != null && usernameToken.Type == JTokenType.String)
                {
                    string usernameStr = usernameToken.ToString();
                    if (IsBase64String(usernameStr) && usernameStr.Length > 100)
                    {
                        byte[] bytes = Convert.FromBase64String(usernameStr);
                        using (var ms = new MemoryStream(bytes))
                        {
                            BinaryFormatter bf = new BinaryFormatter();
                            object deserializedObject = bf.Deserialize(ms);
                            username = deserializedObject.ToString();
                        }
                    }
                    else
                    {
                        username = usernameStr;
                    }
                }

                password = passwordToken?.ToString();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    return Json(new { success = false, message = "Invalid input" });
                }

                if (users.Any(u => u.Username == username))
                {
                    return Json(new { success = false, message = "Username already exists" });
                }

                var newUser = new User
                {
                    Id = users.Max(u => u.Id) + 1,
                    Username = username,
                    Password = password
                };
                users.Add(newUser);
                return Json(new { success = true, message = "Signup successful" });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: Signup Error: {ex.Message}");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private bool IsBase64String(string base64)
        {
            if (string.IsNullOrEmpty(base64) || base64.Length % 4 != 0)
            {
                return false;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(base64, @"^[A-Za-z0-9\+/]*={0,2}$"))
            {
                return false;
            }

            try
            {
                Convert.FromBase64String(base64);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }
    }
}