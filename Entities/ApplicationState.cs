using System;
using System.Collections.Generic;
using System.Globalization;

namespace SapphireDiffApplier
{
    public abstract class BaseApplicationState
    {
        // Даты
        public DateTime LastApplierDate { get; set; }
        public DateTime LastDownloadDate { get; protected set; }
        protected string _secondaryApplierDate;

        // Состояния
        public bool IsOkay { get; set; }
        public bool NeedsUpdateStatus => !this.LastApplierDate.Equals(this.LastDownloadDate);

        // Статусы применения дифов
        public Dictionary<string, DayStatus> DayStatuses { get; }

        // Данные модели
        public int ModelNum { get; private set; }
        public bool ModelFound { get; private set; }
        public string Description { get; set; } 
        
        // Ошибки
        public Exception Exception { get; set; }
        public string ExceptionReason { get; set; }

        public BaseApplicationState(ApplyStatus applyStatus) 
        { 
            this.IsOkay = true;
            this.LastDownloadDate = DateTime.ParseExact(applyStatus.lssd, "yyyy-MM-dd", new CultureInfo("en-US"));
            this.DayStatuses = new Dictionary<string, DayStatus>();
            this.ExceptionReason = string.Empty;
        }

        public void SetModelSeeker(int modelNum, bool isFound)
        {
            this.ModelFound = isFound;
            this.ModelNum = modelNum;
        }

        public void AddDayStatus(string date, DayStatus dayStatus)
        {
            this.DayStatuses.Add(date, dayStatus);
        }

        public abstract ApplyStatus GetApplyStatus();

        public void SendEmail() 
        { 
            MailSender.SendEmail(this); 
        }
    }

    public class ApplicationStateGost : BaseApplicationState
    {
        public ApplicationStateGost(ApplyStatus applyStatus) : base(applyStatus) 
        { 
            this.LastApplierDate = DateTime.ParseExact(applyStatus.lsad_Gost, "yyyy-MM-dd", new CultureInfo("en-US"));
            this._secondaryApplierDate = applyStatus.lsad_NonGost;
            this.Description = "стандартный импорт";
        }

        public override ApplyStatus GetApplyStatus()
        {
            var applyStatus = new ApplyStatus();
            applyStatus.lssd = this.LastDownloadDate.ToString("yyyy-MM-dd");
            applyStatus.lsad_Gost = this.LastApplierDate.ToString("yyyy-MM-dd");
            applyStatus.lsad_NonGost = this._secondaryApplierDate;

            return applyStatus;
        }
    }

    public class ApplicationStateNonGost : BaseApplicationState
    {
        public ApplicationStateNonGost(ApplyStatus applyStatus) : base(applyStatus)
        {
            this.LastApplierDate = DateTime.ParseExact(applyStatus.lsad_NonGost, "yyyy-MM-dd", new CultureInfo("en-US"));
            this._secondaryApplierDate = applyStatus.lsad_Gost;
            this.Description = "специальный импорт по ГОСТ СО";
        }

        public override ApplyStatus GetApplyStatus()
        {
            var applyStatus = new ApplyStatus();
            applyStatus.lssd = this.LastDownloadDate.ToString("yyyy-MM-dd");
            applyStatus.lsad_Gost = this._secondaryApplierDate;
            applyStatus.lsad_NonGost = this.LastApplierDate.ToString("yyyy-MM-dd");

            return applyStatus;
        }
    }
}
