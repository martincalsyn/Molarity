using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Molarity.Hosting;

namespace Molarity.CommandLineHost
{
    class Program
    {
        private static ServiceHost _host;
        static void Main(string[] args)
        {
            _host = ServiceHost.Create().Result;
            _host.Start();
            Console.WriteLine("Press ENTER to stop the program");
            Console.ReadLine();
        }
    }
}
