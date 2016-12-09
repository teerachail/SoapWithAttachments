using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace MtomTestSite
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IService1
    {
        public string GetData(int value)
        {
            return string.Format("You entered: {0}", value);
        }

        public string GetDataLen(string name, byte[] data)
        {
            return string.Format("{0}: {1}", name, data.Length);
        }

        public string GetDataLenStream(Stream data)
        {
            byte[] buffer = new byte[2048];
            var n = data.Read(buffer, 0, buffer.Length);
            return string.Format("Data Length: {0}", n);
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite == null)
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        public MyMtomData GetMyMtomData()
        {
            return new MtomTestSite.MyMtomData
            {
                Name = string.Format("My Mtom Data @{0}", DateTime.Now),
                File1 = new byte[21500],
                File2 = new byte[58009],
            };
        }

        public MyMtomData RoundTripMyMtomData(MyMtomData data)
        {
            data.Name = string.Format("Received: {0}", data.Name);
            return data;
        }
    }
}
