using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WcfClientConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var svc = new ServiceReference1.Service1Client())
            {
                var rsp = svc.GetData(200);
                Console.WriteLine(rsp);
            }
        }
    }
}
