using System;
using System.Collections.Generic;
using RuhsatHesap.Core.Model;

namespace RuhsatHesap.Core
{
    public sealed class CalculationSummary
    {
        public double MaxFootprint;            // Azami TAKS alanı
        public double MaxEmsal;                // Azami emsal alanı
        public double CalculatedEmsal;         // Hesaplanan emsal alanı
        public double EmsalExcess;             // Emsal aşımı
        public double EmsalBalance;            // Kalan emsal hakkı
        public double ThirtyPercentTotal;      // %30 istisna tablosundaki toplam
        public double EmsalOutsideTotal;       // Emsal dışı alan toplamı
        public double ConstructionArea;        // Yapı inşaat alanı
        public double RetainingWallArea;
        public double ConstructionGrandTotal;
        public int RequiredTrees;
        public int RequiredParkingSpaces;
        public int UnitCount;

        public bool EmsalOk => EmsalExcess <= 0.0;
        public bool ParkingOk (int provided) => provided >= RequiredParkingSpaces;
    }

    /// <summary>
    /// Port of Src/CalculationEngine.cpp. The numbers produced here are
    /// deliberately identical to the Archicad add-on and the web panel: the
    /// %30 istisna table is reported but, exactly as in those two, it is not
    /// silently added to the emsal total.
    /// </summary>
    public static class CalculationEngine
    {
        public static double ParkingContribution (double grossArea)
        {
            if (grossArea <= 0.0) return 0.0;
            if (grossArea < 80.0) return 1.0 / 3.0;
            if (grossArea < 120.0) return 1.0 / 2.0;
            if (grossArea < 180.0) return 1.0;
            return 2.0;
        }

        public static double SumValues (Dictionary<string, double> values)
        {
            double total = 0.0;
            foreach (double value in values.Values) total += value;
            return total;
        }

        public static CalculationSummary Calculate (ProjectData project)
        {
            var result = new CalculationSummary ();
            result.MaxFootprint = project.Parcel.ParcelArea * project.Parcel.TaksRate;
            result.MaxEmsal = string.Equals (project.Parcel.EmsalMethod, "direct", StringComparison.OrdinalIgnoreCase)
                ? project.Parcel.DirectEmsal
                : project.Parcel.ParcelArea * project.Parcel.KaksRate;

            double rawParking = 0.0;
            foreach (BlockRecord block in project.Blocks) {
                foreach (FloorRecord floor in block.Floors) {
                    result.CalculatedEmsal += floor.EmsalArea;
                    result.EmsalOutsideTotal += floor.EmsalOutsideArea;
                    result.ThirtyPercentTotal += SumValues (floor.ThirtyPercentAreas);
                    result.ConstructionArea += SumValues (floor.ConstructionAreas);
                    result.ConstructionArea += block.UnitGrossOnFloor (floor.Name);
                }
                foreach (IndependentUnit unit in block.Units) {
                    result.UnitCount++;
                    rawParking += ParkingContribution (unit.GrossArea);
                }
            }

            foreach (RetainingWall wall in project.RetainingWalls) result.RetainingWallArea += wall.Area;

            result.EmsalExcess = Math.Max (0.0, result.CalculatedEmsal - result.MaxEmsal);
            result.EmsalBalance = Math.Max (0.0, result.MaxEmsal - result.CalculatedEmsal);
            result.ConstructionGrandTotal = result.ConstructionArea + result.RetainingWallArea;
            double gardenArea = Math.Max (0.0, project.Parcel.ParcelArea - project.Parcel.BuildingFootprint);
            result.RequiredTrees = project.Parcel.ParcelArea > 0.0 ? (int) Math.Ceiling (gardenArea / 30.0) : 0;
            result.RequiredParkingSpaces = (int) Math.Ceiling (rawParking);
            return result;
        }
    }
}
