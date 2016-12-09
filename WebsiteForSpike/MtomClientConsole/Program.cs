using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MtomClientConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var svc = new LocalServiceReference1.Service1Client("BasicHttpBinding_IService11"))
            {
                var rsp1 = svc.GetDataLen("From GetDataLen", new byte[256]);
                Console.WriteLine(rsp1);

                var ms = new MemoryStream(new byte[512]);
                var rsp2 = svc.GetDataLenStream(ms);
                Console.WriteLine(rsp2);
            }
            using (var svc = new ServiceReference1.Service1Client("BasicHttpsBinding_IService1"))
            {
                var rsp1 = svc.GetDataLen("From GetDataLen", new byte[256]);
                Console.WriteLine(rsp1);

                var ms = new MemoryStream(new byte[512]);
                var rsp2 = svc.GetDataLenStream(ms);
                Console.WriteLine(rsp2);
            }
        }
    }
}
