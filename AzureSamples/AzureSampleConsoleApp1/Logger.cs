using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureSampleConsoleApp1
{
    public class Logger : ILogger
    {
        public void WriteLine(string message)
        {
            Debug.WriteLine(message);
        }
    }

    public interface ILogger
    {
        void WriteLine(string message);
    }
}
