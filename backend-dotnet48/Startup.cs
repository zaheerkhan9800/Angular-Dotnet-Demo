using System;
using System.Web.Http;

namespace BackendDotnet48
{
    public class Program
    {
        static void Main(string[] args)
        {
            string baseAddress = "http://localhost:5000/";

            using (Microsoft.Owin.Hosting.WebApp.Start<Startup>(url: baseAddress))
            {
                Console.WriteLine("Server is running at " + baseAddress);
                Console.WriteLine("Press Enter to stop the server...");
                Console.ReadLine();
            }
        }
    }
}
