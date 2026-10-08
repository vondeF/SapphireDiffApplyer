using Monitel.ObjectDb;
using System;
using System.Collections.Generic;
using System.Linq;
using Monitel.Mal.Providers;
using Monitel.Mal.Providers.Mal;
using Monitel.Mal;
using System.IO;
using Newtonsoft.Json;
using System.Xml;
using Monitel.Serialization.CIMXML;
using System.Text.RegularExpressions;
using Monitel.PlatformInfrastructure.Logger;
using Monitel.Serialization.CIMXML.DiffModel;
using Serilog;
using System.Reflection;
using Monitel.PlatformInfrastructure.Converters;
using Monitel.Extensions.Logging.PlatformLogger;
using Monitel.MappingService.Client;

namespace SapphireDiffApplier
{
    public abstract class BaseApplier
    {
        public BaseApplicationState ApplicationState { get; }
        protected string _serverName;
        protected string _odbName;
        protected string _moduleInfo;
        protected DateTime _currentDate;
        protected DateTime _endDate;
        protected string _directoryPath;
        protected ILogger _logger;

        public BaseApplier(string serverName, string odbName, string directoryPath, BaseApplicationState applicationState, ILogger logger)
        {
            this._serverName = serverName;
            this._odbName = odbName;
            this._directoryPath = directoryPath;
            this.ApplicationState = applicationState;
            this._logger = logger;
            this._moduleInfo = "DiffApplier";

            this._logger.Information("Инициализация сервиса импорта. Параметры: Сервер {Server}, БД {DataBase}, Описание - {Description}", serverName, odbName, applicationState.Description);
        }

        protected void SetModelNumber()
        {
            this._logger.Information("Поиск модели для применения наборов изменений");
            var svc = OdbServiceFactory.CreateServerObject(_serverName);
            var inst = svc.GetInstanceByName(_serverName, _odbName);
            var dbList = svc.GetAllModelVersions(inst);
            var actual = dbList.FirstOrDefault(x => x.IsActual);
            var children = dbList.Where(x => x.BaseModelVersionId == actual.Id);
            var toApply = children.FirstOrDefault(x => x.Description?.ToLower() == "4diff");

            if (toApply == null)
            {
                var model = svc.CreateModelVersion(inst, "Автоматическое применение фрагментов", actual.Id, null, false, "4diff");
                this.ApplicationState.SetModelSeeker(model, false);
                this._logger.Information("Модель не найдена. Создана модель с номером {Number}", model);
            }
            else
            {
                this.ApplicationState.SetModelSeeker(toApply.Id, true);
                this._logger.Information("Модель найдена. Номер модели {Number}", toApply.Id);
            }
        }

        public void Import()
        {
            try
            {
                if (this.ApplicationState.NeedsUpdateStatus)
                {
                    SetModelNumber();
                    var currentDate = this.ApplicationState.LastApplierDate;
                    var endDate = this.ApplicationState.LastDownloadDate;
                    var directories = Directory.GetDirectories(_directoryPath);

                    while (currentDate != endDate)
                    {
                        currentDate = currentDate.AddDays(1);
                        this._logger.Information("Начало импорта данных за дату {Date}", currentDate);
                        var folderName = directories.FirstOrDefault(x => x.EndsWith(currentDate.ToString("yyyy-MM-dd")));

                        DayStatus dayStatus = null;
                        if (folderName != null)
                        {
                            dayStatus = JsonConvert.DeserializeObject<DayStatus>(File.ReadAllText(folderName + "\\dayStatus.json"));
                            if (dayStatus.IsOkay == false)
                            {
                                this._logger.Warning("Загрузка наборов изменений ЛСА SLON была завершена с ошибкой. Необходимо загрузить файлы корректно");
                                this.ApplicationState.IsOkay = false;
                                break;
                            }
                            else
                            {
                                ApplyDayDiffs(folderName, out int numOfDiffs, out int cancelledDiffs);
                                dayStatus.NumOfDiffs = numOfDiffs;
                                dayStatus.CancelledDiffs = cancelledDiffs;
                                this._logger.Information("Импорт данных за дату {Date} завершен успешно. Импортировано {Number1} наборов, отменено - {Number2}", currentDate, numOfDiffs, cancelledDiffs);
                            }
                        }
                        this.ApplicationState.AddDayStatus(currentDate.ToString("yyyy-MM-dd"), dayStatus);
                        this.ApplicationState.LastApplierDate = currentDate;
                    }
                }
                this._logger.Information("Дата последнего успешного импорта совпадает с датой последней успешной загрузки наоборов изменений из CIM-портала");
            }
            catch (ModelTransactionException e)
            {
                var reason = "Исключение при транзакции в ИМ";
                this.ApplicationState.IsOkay = false;
                this.ApplicationState.Exception = e;
                this.ApplicationState.ExceptionReason = reason;
                this._logger.Error(e, reason);
            }
            catch (DiffApplierException e)
            {
                var reason = $"Исключение при применении набора изменений. Последний прочитанный файл: {e.FileName}";
                this.ApplicationState.IsOkay = false;
                this.ApplicationState.Exception = e;
                this.ApplicationState.ExceptionReason = reason;
                this._logger.Error(e, reason);
            }
            catch (Exception e)
            {
                var reason = "Необработанное исключение";
                this.ApplicationState.IsOkay = false;
                this.ApplicationState.Exception = e;
                this.ApplicationState.ExceptionReason = reason;
                this._logger.Error(e, reason);
            }
            this.ApplicationState.SendEmail();
            this._logger.Information("Письмо с результатами импорта направлено на почту отвественным лицам");
        }

        protected void ApplyDayDiffs(string parentFolderName, out int numOfDiffs, out int canceledDiffs)
        {
            var fileList = Directory.GetFiles(parentFolderName);
            var file = "";

            numOfDiffs = 0;
            canceledDiffs = 0;

            this._logger.Information("Загрузка версии ИМ для применение наборов изменений");
            MalContextParams contextParams = new MalContextParams()
            {
                OdbServerName = this._serverName,
                OdbInstanseName = this._odbName,
                OdbModelVersionId = this.ApplicationState.ModelNum
            };
            var prov = MalProvider.CreateProviderAsync(contextParams, MalContextMode.Open, _moduleInfo).Result;
            MalProvider dataProvider = new MalProvider(contextParams, MalContextMode.Open, _moduleInfo);
            var modelImage = new ModelImage(dataProvider, false);
            this._logger.Information("Версии ИМ для применение наборов изменений загружена");

            try
            {
                var filesPairs = fileList.Where(x => x.EndsWith(".xml")).Select(x => new Tuple<int, string>(int.Parse(x.Replace(parentFolderName + "\\", "").Split('_')[0]), x));
                foreach (var filePair in filesPairs.OrderBy(x => x.Item1))
                {
                    file = filePair.Item2;
                    var settings = GetSettings(modelImage);
                    ProcessDiff(modelImage, file, settings.DeserializeOptions, settings.ApplySettings);

                    if (file.Replace(parentFolderName + "\\", "").Split('_')[1] == "cancel")
                        canceledDiffs++;
                    numOfDiffs++;
                }
            }
            catch (Exception e) { throw new DiffApplierException(e, file); }
        }

        protected abstract void ProcessDiff(ModelImage modelImage, string filePath, DmDeserializeOptions deserializeOptions, DiffApplySettings applySettings);
        protected abstract (DmDeserializeOptions DeserializeOptions, DiffApplySettings ApplySettings) GetSettings(ModelImage modelImage);

        protected DifferenceModel RemoveTrash(DifferenceModel dm)
        {
            List<Tuple<ClassProperty, DifferenceObject>> removeCollection = new List<Tuple<ClassProperty, DifferenceObject>>();
            foreach (var obj in dm.Forward.All.Where(x => x.Properties.Any(y => y.Kind == PropertyKind.Attribute && y.StoredType == PrimitiveType.Float64)))
            {
                foreach (var prop in obj.Properties)
                {
                    if (prop.Kind == PropertyKind.Attribute && prop.StoredType == PrimitiveType.Float64)
                    {
                        try
                        {
                            var val = obj.GetFloat64((ClassAttribute)prop);
                            if (double.IsNaN(val) || double.IsInfinity(val))
                                removeCollection.Add(new Tuple<ClassProperty, DifferenceObject>(prop, obj));
                        }
                        catch (Exception e) { }
                    }
                }
            }
            if (removeCollection.Any())
            {
                foreach (var p in removeCollection)
                    p.Item2.RemoveEntire(p.Item1);
            }

            // Убираем изменения по системным объектам
            var forwardDelete = dm.Forward.All.Where(x => x.ObjectUid.ToString().EndsWith("-0000-0000-c000-0000006d746c")).ToList();
            foreach (var obj in forwardDelete)
                dm.Forward.Delete(obj);
            var reverseDelete = dm.Reverse.All.Where(x => x.ObjectUid.ToString().EndsWith("-0000-0000-c000-0000006d746c")).ToList();
            foreach (var obj in reverseDelete)
                dm.Reverse.Delete(obj);

            return dm;
        }
    }


    /// <summary>
    /// Применение наборов изменений по обычному импорту
    /// </summary>
    public class ApplierGost : BaseApplier
    {
        public ApplierGost(string serverName, string odbName, string directoryPath, BaseApplicationState applicationState, ILogger logger) : base(serverName, odbName, directoryPath, applicationState, logger) { }

        protected override void ProcessDiff(ModelImage modelImage, string filePath, DmDeserializeOptions deserializeOptions, DiffApplySettings applySettings)
        {
            this._logger.Information("Начало импорта набора изменений из файла {File}", filePath);
            DifferenceModel dm = new DifferenceModel(modelImage.MetaData);
            using (var fs = XmlReader.Create(filePath))
            {
                dm.ImportFromXml(fs, null);
            }
            this._logger.Information("Объект DifferenceModel сформирован", filePath);
            dm.DeleteEmptyDifferenceObjects();
            dm.InquireUnknownObjectClassFrom(modelImage);

            // Убираем создание CurveData, RatioTapChangerTablePoint и других временных данных, которые не могут храниться в БД без преобразования по ГОСТ
            var forwardDelete = dm.Forward.All.Where(x => x.IsDescription == false && (x.ObjectClass == null || x.ObjectClass.ToString() == "CurveData" || x.ObjectClass.ToString() == "RatioTapChangerTablePoint" || x.ObjectClass.IsTransient)).ToList();
            foreach (var obj in forwardDelete)
                dm.Forward.Delete(obj);

            dm = RemoveTrash(dm);

            // Убираем удаление CurveData, RatioTapChangerTablePoint и других временных данных, которые не могут храниться в БД без преобразования по ГОСТ
            var reverseDelete = dm.Reverse.All.Where(x => x.IsDescription == false && (x.ObjectClass == null || x.ObjectClass.ToString() == "CurveData" || x.ObjectClass.ToString() == "RatioTapChangerTablePoint" || x.ObjectClass.IsTransient)).ToList();
            foreach (var obj in reverseDelete)
                dm.Reverse.Delete(obj);

            modelImage.BeginTransaction();
            dm.ApplyTo(modelImage, null);
            this._logger.Information("Импорт набора изменений из файла {File} завершен успешно", filePath);

            try { modelImage.CommitTransaction(); }
            catch (Exception e) { throw new ModelTransactionException(e); }
        }

        protected override (DmDeserializeOptions DeserializeOptions, DiffApplySettings ApplySettings) GetSettings(ModelImage modelImage) => (null, null);
    }

    /// <summary>
    /// Применение наборов изменений по спецциальному импорту ГОСТ СО
    /// </summary>
    public class ApplierNonGost : BaseApplier
    {
        public ApplierNonGost(string serverName, string odbName, string directoryPath, BaseApplicationState applicationState, ILogger logger) : base(serverName, odbName, directoryPath, applicationState, logger) { }

        protected override void ProcessDiff(ModelImage modelImage, string filePath, DmDeserializeOptions deserializeOptions, DiffApplySettings applySettings)
        {
            this._logger.Information("Начало импорта набора изменений из файла {File}", filePath);
            DifferenceModel dm = new DifferenceModel(modelImage.MetaData);
            using (var fs = XmlReader.Create(filePath))
            {
                modelImage.BeginTransaction();
                dm.ImportFromXml(fs, deserializeOptions, null);
                
                try { modelImage.CommitTransaction(); }
                catch (Exception e) { throw new ModelTransactionException(e); }
            }
            this._logger.Information("Объект DifferenceModel сформирован", filePath);

            dm.DeleteEmptyDifferenceObjects();
            dm.InquireUnknownObjectClassFrom(modelImage);
            dm = RemoveTrash(dm);

            modelImage.BeginTransaction();
            dm.ApplyTo(modelImage, null, applySettings);
            this._logger.Information("Импорт набора изменений из файла {File} завершен успешно", filePath);

            try { modelImage.CommitTransaction(); }
            catch (Exception e) { throw new ModelTransactionException(e); }
        }

        protected override (DmDeserializeOptions DeserializeOptions, DiffApplySettings ApplySettings) GetSettings(ModelImage modelImage)
        {
            var modelNavigatorPath = @"C:\Program Files\Monitel\CK-11\Client";
            var connectionStringMappingService = "https://ia-sm-sipr1.cdu.so/api/mappings";

            // Выбор модуля импорта
            var allProviderInfos = RdfProviderManager.GetProviders(modelImage.MetaData, modelNavigatorPath).ToArray<RdfProviderInfo>();
            var providerInfo = GetTargetProviderInfo(allProviderInfos);
            this.ApplicationState.Description = $"специальный импорт по ГОСТ СО (модуль \"{providerInfo.Name}\")";
            var provider = RdfProviderManager.CreateProviderObject(providerInfo);
            var processor = provider.CreateDiffImportProcessor();

            // Настройка формирования набора
            var logger = new DummyLogger(true);
            List<string> errorList = new List<string>();
            List<string> warningList = new List<string>();
            var mappingCollection = new ServiceMappingCollection(connectionStringMappingService, null);

            DmDeserializeOptions deserializeOptions = new DmDeserializeOptions
            {
                Processor = processor,
                TargetModel = modelImage,
                Logger = logger,
                MappingsCollection = mappingCollection,
                Warnings = warningList,
                Errors = errorList
            };

            // Настройка импорта
            DiffApplySettings applySettings = new DiffApplySettings
            {
                IsCompare = false,
                IsSetRoot = true,
                UseMrid = true,
                StrongType = false
            };

            return (deserializeOptions, applySettings);
        }

        protected RdfProviderInfo GetTargetProviderInfo(RdfProviderInfo[] allProviderInfos)
        {
            var soProviderInfos = allProviderInfos
                .Where(x => x.Name.Contains("СО ЕЭС") && x.Name.Contains("(без формы)"));

            var targetProviderInfo = soProviderInfos
                .Select(prov => new { prov, m = Regex.Match(prov.Name, @"\b(\d{2})\.(\d{2})\.(\d{4})\b") })
                .Where(x => x.m.Success)
                .Select(x => new { x.prov, d = DateTime.ParseExact(x.m.Value, "dd.MM.yyyy", null) })
                .OrderByDescending(x => x.d)
                .FirstOrDefault()?.prov;

            this._logger.Information("Выбран модуль специального импорта {Name}", targetProviderInfo.Name);
            return targetProviderInfo;
        }
    }
}
