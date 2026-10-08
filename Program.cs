using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;
using Serilog;
using Microsoft.Extensions.Configuration;
using SapphireDiffApplyer.Entities;
using MimeKit;


namespace SapphireDiffApplier
{
    class Program
    {
        private static AppSettings _settings;

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string folderPath = @"C:\Program Files\Monitel\CK-11\Client";
            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            string assemblyPath = Path.Combine(folderPath, assemblyName);
            if (!assemblyName.StartsWith("EPPlus"))
            {
                // Пытаемся загрузить из указанной папки
                if (File.Exists(assemblyPath))
                    return Assembly.LoadFrom(assemblyPath);
            }
            // Если не найдено, пытаемся загрузить локально (из папки с исполняемым файлом)
            string localAssemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName);
            if (File.Exists(localAssemblyPath))
                return Assembly.LoadFrom(localAssemblyPath);
            // Если сборка не найдена нигде, возвращаем null
            return null;
        }

        static void Main(string[] args)
        {
            // Подтягиваем все библиотеки
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            // Читаем файл с конфигурацией
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Настройки приложения
            _settings = configuration.GetSection("AppSettings").Get<AppSettings>();

            // Логгер
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            Log.Information("Приложение запущено");

            // Настройка отправщика писем
            MailSender.Addresses = _settings.EmailRecipients.Select(x => new MailboxAddress(x.Name, x.Email)).ToArray();

            // Импорт
            ApplyStatus applyStatus;
            try
            {
                // Стандартный импорт
                applyStatus = GetApllyStatus();
                var applierGost = new ApplierGost(
                    _settings.ConnectionStrings.GostServerName, 
                    _settings.DatabaseNames.GostDbName, 
                    _settings.DirectoryPath, 
                    new ApplicationStateGost(applyStatus),
                    Log.Logger);
                applierGost.Import();
                SaveApplyStatus(applierGost.ApplicationState.GetApplyStatus());

                // Импорт по ГОСТ
                applyStatus = GetApllyStatus();
                var applierNonGost = new ApplierNonGost(
                    _settings.ConnectionStrings.NonGostServerName, 
                    _settings.DatabaseNames.NonGostDbName, 
                    _settings.DirectoryPath, 
                    new ApplicationStateNonGost(applyStatus),
                    Log.Logger);
                applierNonGost.Import();
                SaveApplyStatus(applierNonGost.ApplicationState.GetApplyStatus());
            }
            catch (Exception e)
            {
                Log.Error(e, "Критическая ошибка при работе приложения");
            }
            finally
            {
                Log.Information("Приложение завершило работу" + Environment.NewLine);
                Log.CloseAndFlush();
            }
        }

        private static ApplyStatus GetApllyStatus()
        {
            var folder = Directory.GetFiles(_settings.DirectoryPath);
            var fileName = _settings.StatusFilePath;
            if (folder.Contains(fileName))
            {
                var status = JsonConvert.DeserializeObject<ApplyStatus>(File.ReadAllText(folder.First(x => x == fileName)));
                Log.Information("Статус загрузки и применения наборов изменений получен");
                return status;
            }
            else
                throw new Exception("Статус загрузки и применения наборов изменений не найден");
        }

        private static void SaveApplyStatus(ApplyStatus applyStatus)
        {
            var fileName = _settings.StatusFilePath;
            using (StreamWriter sw = File.CreateText(fileName))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Newtonsoft.Json.Formatting.Indented;
                serializer.Serialize(sw, applyStatus);
            }
        }
    }
}
