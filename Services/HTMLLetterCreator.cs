using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SapphireDiffApplier
{
    public class HTMLLetterCreator
    {
        private BaseApplicationState _state;
        private bool _isGost;
        private string _headerColor;

        public HTMLLetterCreator(BaseApplicationState state)
        {
            this._state = state;
            this._isGost = !state.Description.Contains("ГОСТ");
            this._headerColor = _isGost ? "#003366" : "#00541f";
        }

        public string GetMessageText()
        {
            var head = $@"<!DOCTYPE html>
<html lang=""ru"" xmlns=""http://www.w3.org/1999/xhtml"">
<head>
<meta charset=""UTF-8"" />
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
<title>Уведомление о загрузке файлов</title>
<style>
  body {{
    margin:0; 
    padding:0; 
    background-color:#f2f2f2; 
    font-family: Arial, sans-serif;
  }}
  table {{
    border-collapse: collapse; 
    width:100%;
  }}
  .container {{
    width:100%; 
    max-width:600px; 
    margin:0 auto; 
    background:#ffffff;
  }}
  .header {{
    background:{this._headerColor}; 
    color:#ffffff; 
    padding:20px;
    text-align:center;
  }}
  .header img {{
    max-height:50px; 
    margin-bottom:10px;
  }}
  .header h1 {{
    font-size:24px; 
    margin:0;
  }}
  .header h2 {{
    font-size:14px; 
    margin:0;
    text-align:center;
  }}
.header h3 {{
    font-size:12px; 
    margin:0;
    text-align:center;
  }}
  .content {{
    padding:20px;
    color:#333333;
    font-size:16px;
    line-height:1.5;
  }}
  .content p {{
    margin-top:0;
  }}
  .cta {{
    text-align:center; 
    padding:20px;
  }}
  .cta a {{
    display:inline-block; 
    background:#003366; 
    color:#ffffff; 
    text-decoration:none; 
    padding:15px 30px; 
    border-radius:5px; 
    font-weight:bold; 
    font-size:16px;
  }}
  .footer {{
    background:#f2f2f2; 
    text-align:center; 
    color:#777777; 
    font-size:12px; 
    padding:20px;
  }}
  .footer a {{
    color:#003366; 
    text-decoration:none;
  }}
</style>
</head>
";
            var body = GetBody();
            return head + body;
        }

        private string GetBody()
        {
            if (this._state.IsOkay && !this._state.DayStatuses.Any())
            {
                return $@"<body>
  <table>
    <tr>
      <td>
        <table class=""container"">
          <tr>
            <td class=""header"">
              <h1>Отсутствуют новые наборы для применения в модель CIM-Портала {(this._isGost ? "(ИМ по ГОСТ)" : "(ИМ не по ГОСТ)")} в ИУС ""САПФИР-М""</h1>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
            }
            else return
            $@" <body>
  <table>
    <tr>
      <td>
        <table class=""container"">
          <tr>
            <td class=""header"">
              {GetCommonStatus()}
            </td>
          </tr>
          <tr>
            <td class=""content"">
              {GetModelSearchStatus()}
            </td>
          </tr>
          <tr>
            <td class=""content"">
              {GetImportStatus()}
            </td>
          </tr>
          <tr>
            <td class=""footer"">
              <p>Вы получили это письмо, так как ваша учётная запись в системе была отмечена как ответственная за данный проект.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
        }

        private string GetModelSearchStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<h2>Версия модели: {this._state.ModelNum}</h2>");

            if (this._state.ModelFound)
                sb.AppendLine("<h3>Была обнаружена версия модели для применения изменений.</h3>");
            else
                sb.AppendLine("<h3>Версия модели для применения изменений не была обнаружена. Создана новая версия модели.</h3>");

            sb.AppendLine("<h3>Настройки импорта: " + this._state.Description + "</h3>");
            return sb.ToString();
        }

        private string GetCommonStatus()
        {
            if (this._state.IsOkay)
                return $@"<h1>Модель CIM-Портала {(_isGost ? "(ИМ по ГОСТ)" : "(ИМ не по ГОСТ)")} в ИУС ""САПФИР-М"" успешно синхронизирована с полученными наборами изменений</h1>";
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($@"<h1>Модель CIM-Портала {(_isGost ? "(ИМ по ГОСТ)" : "(ИМ не по ГОСТ)")} в ИУС ""САПФИР-М"" не синхронизирована с полученными наборами изменений</h1>");
                sb.AppendLine("<h2>-</h2>");

                if (this._state.Exception == null)
                    sb.AppendLine("<h2>Причина: наличие ошибок в сохраненных наборах изменений</h2>");
                else
                {
                    sb.AppendLine("<h2>Причина:" + this._state.ExceptionReason + "</h2>");
                    sb.AppendLine("<h2>"+ this._state.Exception?.Message +"</h2>");
                }
                return sb.ToString();
            }
        }

        private string GetImportStatus()
        {
            var sb = new StringBuilder();
            if (this._state.DayStatuses.Any())
            {
                sb.AppendLine("<p>Были успешно загружены наборы изменений за следующие даты:</p>");
                sb.AppendLine("<ul>");
                bool broken = false;

                foreach (var r in this._state.DayStatuses)
                {
                    if (r.Value.IsOkay)
                        sb.AppendLine($"<li>{r.Key}: {r.Value.NumOfDiffs} наборов изменений, из них отмененных: {r.Value.CancelledDiffs}</li>");
                    else
                    {
                        sb.AppendLine("</ul>");
                        broken = true;
                        sb.AppendLine($"<p>Набор (-ы) изменений за {r.Key} с id {string.Join(", ", r.Value.ErrorIds)} содержит ошибки</p>");
                        break;
                    }
                }
                if (!broken)
                    sb.AppendLine("</ul>");
            }
            return sb.ToString();
        }
    }
}
