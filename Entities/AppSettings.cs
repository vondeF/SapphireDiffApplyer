using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplyer.Entities
{
    public class AppSettings
    {
        public string DirectoryPath { get; set; }
        public string StatusFilePath { get; set; }
        public ConnectionStrings ConnectionStrings { get; set; }
        public DatabaseNames DatabaseNames { get; set; }
        public List<Recipient> EmailRecipients { get; set; }
    }

    public class ConnectionStrings
    {
        public string GostServerName { get; set; }
        public string NonGostServerName { get; set; }
    }

    public class DatabaseNames
    {
        public string GostDbName { get; set; }
        public string NonGostDbName { get; set; }
    }

    public class Recipient
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}
