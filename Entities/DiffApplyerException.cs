using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplier
{
    public class DiffApplierException : Exception
    {
        public string FileName { get; }

        public DiffApplierException(Exception innerException, string name) : base("Ошибка при применении DifferenceModel в ИМ", innerException)
        {
            FileName = name;
        }
    }
}
