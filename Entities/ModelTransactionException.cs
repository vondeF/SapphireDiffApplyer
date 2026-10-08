using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplier
{
    public class ModelTransactionException: Exception
    {
        public ModelTransactionException(Exception innerException) : base("Ошибка транзакции модели", innerException)
        {
        }
    }
}
