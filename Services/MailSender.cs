using MailKit.Net.Smtp;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplier
{
    public static class MailSender
    {
        public static MailboxAddress[] Addresses;

        public static void SendEmail(BaseApplicationState state)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Автоматическая рассылка ИУС \"САПФИР-М\"", "sapphire@so-ups.ru"));

            if (Addresses == null)
                return;

            foreach (var m in Addresses)
                message.To.Add(m);

            message.Subject = "Статистика применения наборов изменений";
            HTMLLetterCreator cr = new HTMLLetterCreator(state);
            
            message.Body = new TextPart("html")
            {
                Text = cr.GetMessageText()
            };
            using (var client = new SmtpClient())
            {
                client.Connect("ia-ex-nlb.cdu.so", 25, false);
                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}
