using System;
using RuhsatHesap.Core.Json;
using RuhsatHesap.Core.Model;

namespace RuhsatHesap.Core
{
    /// <summary>
    /// Bina tehlike sınıfı, Binaların Yangından Korunması Hakkında Yönetmelik
    /// md. 92'deki "ilave edilecek yangın dolabı ve hidrant debileri" tablosunun
    /// satırları.
    /// </summary>
    public enum FireHazardClass
    {
        Low,
        Ordinary12,
        Ordinary34,
        High
    }

    /// <summary>
    /// Üç depo hesabının girdileri. Every coefficient is a setting rather than
    /// a constant: kullanma suyu kabulleri idareden idareye değişir, yağış ilden
    /// ile, yangın debileri binadaki sistemlere göre. The defaults are only a
    /// starting point and the report prints each one next to the result, so a
    /// number is never shown without the assumption that produced it.
    /// Stored under auxiliaryData.waterTanks, so it round-trips through the
    /// same JSON the web panel and the Archicad add-on read.
    /// </summary>
    public sealed class WaterTankSettings
    {
        public const string AuxiliaryKey = "waterTanks";

        // Kullanma suyu deposu
        public double PersonsPerUnit = 4.0;
        /// <summary>0 ise kişi sayısı bağımsız bölüm sayısından hesaplanır.</summary>
        public double ManualPersons;
        public double DailyLitresPerPerson = 150.0;
        public double StorageDays = 1.0;
        public double DomesticProvided;

        // Yangın suyu deposu
        public FireHazardClass HazardClass = FireHazardClass.Low;
        /// <summary>Yağmurlama tasarım yoğunluğu, mm/dak (= L/dak·m²). 0: sistem yok.</summary>
        public double SprinklerDensity;
        public double SprinklerArea;
        public bool HasCabinets = true;
        public double CabinetFlow = 100.0;
        public bool HasHydrants;
        public double HydrantFlow = 400.0;
        public double DurationMinutes = 30.0;
        public double FireProvided;

        // Yağmur suyu deposu
        /// <summary>0 ise yapı oturum alanı kullanılır.</summary>
        public double RoofArea;
        public double AnnualRainfall;
        public double RoofCoefficient = 0.8;
        public double FilterEfficiency = 0.9;
        /// <summary>Yıllık toplanabilir suyun depolanacak oranı; yönetmelik alt sınırı 0,06.</summary>
        public double StorageRatio = 0.06;
        public double RainProvided;

        /// <summary>
        /// Sets yangın dolabı debisi, hidrant debisi and süre to the md. 92
        /// row of the current tehlike sınıfı. Called when the user picks a
        /// class; the values stay editable afterwards.
        /// </summary>
        public void ApplyHazardDefaults ()
        {
            switch (HazardClass) {
                case FireHazardClass.Ordinary12:
                    CabinetFlow = 100.0; HydrantFlow = 400.0; DurationMinutes = 60.0; break;
                case FireHazardClass.Ordinary34:
                    CabinetFlow = 100.0; HydrantFlow = 1000.0; DurationMinutes = 60.0; break;
                case FireHazardClass.High:
                    CabinetFlow = 200.0; HydrantFlow = 1500.0; DurationMinutes = 90.0; break;
                default:
                    CabinetFlow = 100.0; HydrantFlow = 400.0; DurationMinutes = 30.0; break;
            }
        }

        public static string HazardLabel (FireHazardClass hazardClass)
        {
            switch (hazardClass) {
                case FireHazardClass.Ordinary12: return "Orta Tehlike 1-2";
                case FireHazardClass.Ordinary34: return "Orta Tehlike 3-4";
                case FireHazardClass.High: return "Yüksek Tehlike";
                default: return "Düşük Tehlike";
            }
        }

        private static string HazardCode (FireHazardClass hazardClass)
        {
            switch (hazardClass) {
                case FireHazardClass.Ordinary12: return "ordinary12";
                case FireHazardClass.Ordinary34: return "ordinary34";
                case FireHazardClass.High: return "high";
                default: return "low";
            }
        }

        private static FireHazardClass ParseHazard (string code)
        {
            switch ((code ?? string.Empty).Trim ().ToLowerInvariant ()) {
                case "ordinary12": return FireHazardClass.Ordinary12;
                case "ordinary34": return FireHazardClass.Ordinary34;
                case "high": return FireHazardClass.High;
                default: return FireHazardClass.Low;
            }
        }

        public static WaterTankSettings Load (ProjectData project)
        {
            var settings = new WaterTankSettings ();
            JsonValue json = project.AuxiliaryData != null ? project.AuxiliaryData[AuxiliaryKey] : JsonValue.Null;
            if (!json.IsObject) return settings;

            settings.PersonsPerUnit = Number (json, "personsPerUnit", settings.PersonsPerUnit);
            settings.ManualPersons = Number (json, "manualPersons", settings.ManualPersons);
            settings.DailyLitresPerPerson = Number (json, "dailyLitresPerPerson", settings.DailyLitresPerPerson);
            settings.StorageDays = Number (json, "storageDays", settings.StorageDays);
            settings.DomesticProvided = Number (json, "domesticProvided", settings.DomesticProvided);

            settings.HazardClass = ParseHazard (json["hazardClass"].AsString ());
            settings.SprinklerDensity = Number (json, "sprinklerDensity", settings.SprinklerDensity);
            settings.SprinklerArea = Number (json, "sprinklerArea", settings.SprinklerArea);
            settings.HasCabinets = json.Has ("hasCabinets") ? json["hasCabinets"].AsBool () : settings.HasCabinets;
            settings.CabinetFlow = Number (json, "cabinetFlow", settings.CabinetFlow);
            settings.HasHydrants = json.Has ("hasHydrants") ? json["hasHydrants"].AsBool () : settings.HasHydrants;
            settings.HydrantFlow = Number (json, "hydrantFlow", settings.HydrantFlow);
            settings.DurationMinutes = Number (json, "durationMinutes", settings.DurationMinutes);
            settings.FireProvided = Number (json, "fireProvided", settings.FireProvided);

            settings.RoofArea = Number (json, "roofArea", settings.RoofArea);
            settings.AnnualRainfall = Number (json, "annualRainfall", settings.AnnualRainfall);
            settings.RoofCoefficient = Number (json, "roofCoefficient", settings.RoofCoefficient);
            settings.FilterEfficiency = Number (json, "filterEfficiency", settings.FilterEfficiency);
            settings.StorageRatio = Number (json, "storageRatio", settings.StorageRatio);
            settings.RainProvided = Number (json, "rainProvided", settings.RainProvided);
            return settings;
        }

        public void Save (ProjectData project)
        {
            if (project.AuxiliaryData == null || !project.AuxiliaryData.IsObject)
                project.AuxiliaryData = JsonValue.NewObject ();

            JsonValue json = JsonValue.NewObject ();
            json["personsPerUnit"] = JsonValue.Number (PersonsPerUnit);
            json["manualPersons"] = JsonValue.Number (ManualPersons);
            json["dailyLitresPerPerson"] = JsonValue.Number (DailyLitresPerPerson);
            json["storageDays"] = JsonValue.Number (StorageDays);
            json["domesticProvided"] = JsonValue.Number (DomesticProvided);

            json["hazardClass"] = JsonValue.String (HazardCode (HazardClass));
            json["sprinklerDensity"] = JsonValue.Number (SprinklerDensity);
            json["sprinklerArea"] = JsonValue.Number (SprinklerArea);
            json["hasCabinets"] = JsonValue.Bool (HasCabinets);
            json["cabinetFlow"] = JsonValue.Number (CabinetFlow);
            json["hasHydrants"] = JsonValue.Bool (HasHydrants);
            json["hydrantFlow"] = JsonValue.Number (HydrantFlow);
            json["durationMinutes"] = JsonValue.Number (DurationMinutes);
            json["fireProvided"] = JsonValue.Number (FireProvided);

            json["roofArea"] = JsonValue.Number (RoofArea);
            json["annualRainfall"] = JsonValue.Number (AnnualRainfall);
            json["roofCoefficient"] = JsonValue.Number (RoofCoefficient);
            json["filterEfficiency"] = JsonValue.Number (FilterEfficiency);
            json["storageRatio"] = JsonValue.Number (StorageRatio);
            json["rainProvided"] = JsonValue.Number (RainProvided);
            project.AuxiliaryData[AuxiliaryKey] = json;
        }

        private static double Number (JsonValue json, string key, double fallback) =>
            json.Has (key) ? json[key].AsDouble (fallback) : fallback;
    }

    public sealed class WaterTankResult
    {
        // Kullanma suyu
        public int UnitCount;
        public double Persons;
        public bool PersonsAreManual;
        public double DomesticRequired;        // m³

        // Yangın suyu
        public double SprinklerFlow;           // L/dak
        public double CabinetFlow;             // L/dak, sistem yoksa 0
        public double HydrantFlow;             // L/dak, sistem yoksa 0
        public double TotalFireFlow;           // L/dak
        public double FireRequired;            // m³

        // Yağmur suyu
        public double RoofArea;                // m²
        public bool RoofAreaFromFootprint;
        public double AnnualYieldLitres;       // L/yıl
        public double RainRequired;            // m³
        /// <summary>Planlı Alanlar İmar Yönetmeliği: 2000 m²'den büyük parseller.</summary>
        public bool RainMandatoryByParcel;

        /// <summary>Sağlanan hacim gerekli hacmi karşılıyor mu (santilitre toleransıyla).</summary>
        public static bool Covers (double provided, double required) => provided + 0.005 >= required;
    }

    public static class WaterTankCalculator
    {
        /// <summary>Planlı Alanlar İmar Yönetmeliği'nin yağmur suyu toplama eşiği, m².</summary>
        public const double RainMandatoryParcelArea = 2000.0;

        public static WaterTankResult Calculate (ProjectData project) =>
            Calculate (project, WaterTankSettings.Load (project));

        public static WaterTankResult Calculate (ProjectData project, WaterTankSettings settings)
        {
            var result = new WaterTankResult ();

            // Kullanma suyu: kişi × günlük tüketim × gün.
            foreach (BlockRecord block in project.Blocks) result.UnitCount += block.Units.Count;
            result.PersonsAreManual = settings.ManualPersons > 0.0;
            result.Persons = result.PersonsAreManual
                ? settings.ManualPersons
                : result.UnitCount * Positive (settings.PersonsPerUnit);
            result.DomesticRequired = Round2 (
                result.Persons * Positive (settings.DailyLitresPerPerson) * Positive (settings.StorageDays) / 1000.0);

            // Yangın suyu: (yağmurlama + yangın dolabı + hidrant) × süre.
            result.SprinklerFlow = Positive (settings.SprinklerDensity) * Positive (settings.SprinklerArea);
            result.CabinetFlow = settings.HasCabinets ? Positive (settings.CabinetFlow) : 0.0;
            result.HydrantFlow = settings.HasHydrants ? Positive (settings.HydrantFlow) : 0.0;
            result.TotalFireFlow = result.SprinklerFlow + result.CabinetFlow + result.HydrantFlow;
            result.FireRequired = Round2 (result.TotalFireFlow * Positive (settings.DurationMinutes) / 1000.0);

            // Yağmur suyu: YR = A × ha × e × η, depo = YR × oran.
            result.RoofAreaFromFootprint = settings.RoofArea <= 0.0;
            result.RoofArea = result.RoofAreaFromFootprint
                ? Positive (project.Parcel.BuildingFootprint)
                : settings.RoofArea;
            result.AnnualYieldLitres = result.RoofArea * Positive (settings.AnnualRainfall) *
                Positive (settings.RoofCoefficient) * Positive (settings.FilterEfficiency);
            result.RainRequired = Round2 (result.AnnualYieldLitres * Positive (settings.StorageRatio) / 1000.0);
            result.RainMandatoryByParcel = project.Parcel.ParcelArea > RainMandatoryParcelArea;
            return result;
        }

        private static double Positive (double value) =>
            double.IsNaN (value) || double.IsInfinity (value) || value < 0.0 ? 0.0 : value;

        private static double Round2 (double value) => Math.Round (value, 2, MidpointRounding.AwayFromZero);
    }
}
